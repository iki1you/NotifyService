using Abstractions.Models;
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

        public async Task PublishAsync(string queueName, MessageTaskDTO messageTask)
        {
            try
            {
                var connection = _connectionFactory.GetConnection();

                using var channel = await connection.CreateChannelAsync();

                // Объявляем очередь (создается, если не существует)
                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Сериализуем сообщение
                var messageJson = JsonSerializer.Serialize(messageTask, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(messageJson);

                // Настройка свойств сообщения
                var properties = new BasicProperties
                {
                    Persistent = true, // Сообщение сохранится на диск
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                // Публикуем сообщение
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                _logger.LogInformation(
                    "Message published to queue {QueueName}. MessageTask ID: {MessageTaskId}, RequestId: {RequestId}",
                    queueName, messageTask.Id, messageTask.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish message to queue {QueueName}. MessageTask ID: {MessageTaskId}, RequestId: {RequestId}",
                    queueName, messageTask.Id, messageTask.RequestId);
                throw;
            }
        }
    }
}

