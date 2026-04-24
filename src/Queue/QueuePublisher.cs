using Microsoft.Extensions.Logging;
using Queue.Constants;
using Queue.Interfaces;
using Queue.Services;
using Queue.Telemetry;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Queue
{
    public class QueuePublisher : IQueuePublisher
    {
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly ILogger<QueuePublisher> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public QueuePublisher(
            IRabbitMqConnectionFactory connectionFactory,
            ILogger<QueuePublisher> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task PublishAsync<T>(string queueName, T message, bool throwOnError = true)
        {
            try
            {
                using var publishActivity = QueueTelemetry.ActivitySource.StartActivity("queue.publish", ActivityKind.Producer);
                publishActivity?.SetTag("messaging.system", "rabbitmq");
                publishActivity?.SetTag("messaging.operation", "publish");
                publishActivity?.SetTag("messaging.destination.name", queueName);
                publishActivity?.SetTag("messaging.destination.kind", "queue");
                publishActivity?.SetTag("messaging.message.type", typeof(T).Name);

                var requestId = TryGetRequestId(message);
                if (!string.IsNullOrWhiteSpace(requestId))
                {
                    publishActivity?.SetTag("request.id", requestId);
                    publishActivity?.AddBaggage("request.id", requestId);
                }

                var connection = _connectionFactory.GetConnection();

                using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: QueueNames.GetQueueArguments(queueName));

                var messageJson = JsonSerializer.Serialize(message, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(messageJson);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                properties.Headers ??= new Dictionary<string, object?>();
                QueueTelemetry.InjectContextIntoHeaders(properties.Headers, publishActivity ?? Activity.Current);

                var propagatedTraceParent = QueueTelemetry.ReadHeaderAsString(properties.Headers, QueueTelemetry.TraceParentHeader);
                var propagatedTraceState = QueueTelemetry.ReadHeaderAsString(properties.Headers, QueueTelemetry.TraceStateHeader);

                if (ActivityContext.TryParse(propagatedTraceParent, propagatedTraceState, isRemote: true, out var propagatedContext))
                {
                    publishActivity?.SetTag("messaging.rabbitmq.trace_id", propagatedContext.TraceId.ToString());
                }

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation(
                    "Message published to queue {QueueName}. Message type: {MessageType}. TraceId={TraceId}",
                    queueName,
                    typeof(T).Name,
                    publishActivity?.TraceId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish message to queue {QueueName}. Message type: {MessageType}",
                    queueName, typeof(T).Name);

                if (throwOnError)
                {
                    throw;
                }
            }
        }

        private static string? TryGetRequestId<TMessage>(TMessage message)
        {
            var requestIdProperty = typeof(TMessage).GetProperty("RequestId");
            var requestIdValue = requestIdProperty?.GetValue(message);

            return requestIdValue switch
            {
                Guid guid when guid != Guid.Empty => guid.ToString(),
                string text when !string.IsNullOrWhiteSpace(text) => text,
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => null
            };
        }
    }
}

