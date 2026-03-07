using Microsoft.Extensions.Logging;
using Queue.Interfaces;
using Queue.Services;
using RabbitMQ.Client;
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
                var connection = _connectionFactory.GetConnection();

                using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                var messageJson = JsonSerializer.Serialize(message, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(messageJson);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation(
                    "Message published to queue {QueueName}. Message type: {MessageType}",
                    queueName, typeof(T).Name);
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
    }
}

