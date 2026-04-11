using Abstractions.Models;
using Microsoft.Extensions.Logging;
using Queue.Interfaces;
using Queue.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Queue
{
    public class QueueConsumer : IQueueConsumer
    {
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly ILogger<QueueConsumer> _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private IChannel? _channel;
        private string? _consumerTag;

        public QueueConsumer(
            IRabbitMqConnectionFactory connectionFactory,
            ILogger<QueueConsumer> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task StartConsuming(string queueName, Func<MessageTaskDTO, Task> messageHandler, CancellationToken cancellationToken)
        {
            try
            {
                var connection = _connectionFactory.GetConnection();
                _channel = await connection.CreateChannelAsync();

                // Объявляем очередь
                await _channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Настройка prefetch (количество сообщений для обработки одновременно)
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

                // Создаем асинхронного потребителя
                var consumer = new AsyncEventingBasicConsumer(_channel);
                
                consumer.ReceivedAsync += async (sender, eventArgs) =>
                {
                    try
                    {
                        var body = eventArgs.Body.ToArray();
                        var messageJson = Encoding.UTF8.GetString(body);
                        
                        _logger.LogDebug("Received message from queue {QueueName}: {Message}", queueName, messageJson);

                        var messageTask = JsonSerializer.Deserialize<MessageTaskDTO>(messageJson, _jsonOptions);
                        
                        if (messageTask != null)
                        {
                            await messageHandler(messageTask);
                            
                            // Подтверждаем обработку сообщения
                            await _channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);
                            
                            _logger.LogInformation(
                                "Message processed successfully from queue {QueueName}. MessageTask ID: {MessageTaskId}",
                                queueName, messageTask.Id);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize message from queue {QueueName}", queueName);
                            
                            // Отклоняем сообщение (requeue = false, чтобы не создавать бесконечный цикл)
                            await _channel.BasicRejectAsync(deliveryTag: eventArgs.DeliveryTag, requeue: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue {QueueName}", queueName);
                        
                        // Возвращаем сообщение в очередь для повторной обработки
                        await _channel.BasicNackAsync(
                            deliveryTag: eventArgs.DeliveryTag,
                            multiple: false,
                            requeue: true);
                    }
                };

                // Начинаем потребление
                _consumerTag = await _channel.BasicConsumeAsync(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("Started consuming from queue {QueueName}", queueName);

                // Ожидаем отмены
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consuming from queue {QueueName} was cancelled", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in consumer for queue {QueueName}", queueName);
                throw;
            }
        }

        public async Task StopConsuming()
        {
            if (_channel != null && !string.IsNullOrEmpty(_consumerTag))
            {
                await _channel.BasicCancelAsync(_consumerTag);
                await _channel.CloseAsync();
                _channel.Dispose();
                _logger.LogInformation("Stopped consuming. Consumer tag: {ConsumerTag}", _consumerTag);
            }
        }

        public async Task DeleteQueue(string queueName)
        {
            try
            {
                var connection = _connectionFactory.GetConnection();
                using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeleteAsync(queueName, ifUnused: false, ifEmpty: false);
                _logger.LogInformation("Queue {QueueName} deleted successfully", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting queue {QueueName}", queueName);
                throw;
            }
        }
    }
}
