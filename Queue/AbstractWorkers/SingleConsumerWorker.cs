using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Queue.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Queue.AbstractWorkers
{
    /// <summary>
    /// Абстрактный базовый класс для воркеров, работающих с одной очередью.
    /// Упрощает создание воркеров для обработки сообщений из одной очереди RabbitMQ.
    /// </summary>
    /// <typeparam name="TMessage">Тип сообщения, которое обрабатывает воркер.</typeparam>
    public abstract class SingleConsumerWorker<TMessage> : BackgroundService where TMessage : class
    {
        private readonly ILogger _logger;
        protected readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IRabbitMqConnectionFactory _connectionFactory;
        private readonly string _queueName;
        private readonly string _workerName;
        private readonly JsonSerializerOptions _jsonOptions;
        private IChannel? _channel;
        private string? _consumerTag;

        /// <summary>
        /// Инициализирует новый экземпляр воркера для работы с одной очередью.
        /// </summary>
        /// <param name="logger">Logger для логирования операций воркера.</param>
        /// <param name="serviceScopeFactory">Factory для создания scopes при работе с scoped зависимостями.</param>
        /// <param name="connectionFactory">Factory для подключения к RabbitMQ.</param>
        /// <param name="queueName">Имя очереди для прослушивания.</param>
        /// <param name="workerName">Имя воркера для логирования.</param>
        /// <param name="prefetchCount">Количество сообщений для одновременной обработки (по умолчанию 10).</param>
        protected SingleConsumerWorker(
            ILogger logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory,
            string queueName,
            string workerName,
            ushort prefetchCount = 10)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _connectionFactory = connectionFactory;
            _queueName = queueName;
            _workerName = workerName;
            PrefetchCount = prefetchCount;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        /// <summary>
        /// Количество сообщений для одновременной обработки.
        /// </summary>
        protected ushort PrefetchCount { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{WorkerName} starting...", _workerName);

            try
            {
                var connection = _connectionFactory.GetConnection();
                _channel = await connection.CreateChannelAsync();

                // Объявляем очередь
                await _channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Настройка prefetch
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false);

                // Создаем асинхронного потребителя
                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.ReceivedAsync += async (sender, eventArgs) =>
                {
                    try
                    {
                        var body = eventArgs.Body.ToArray();
                        var messageJson = Encoding.UTF8.GetString(body);

                        _logger.LogDebug("{WorkerName}: Received message: {Message}", _workerName, messageJson);

                        var message = JsonSerializer.Deserialize<TMessage>(messageJson, _jsonOptions);

                        if (message != null)
                        {
                            await ProcessMessageAsync(message);

                            // Подтверждаем обработку
                            await _channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);

                            _logger.LogDebug("{WorkerName}: Message processed successfully", _workerName);
                        }
                        else
                        {
                            _logger.LogWarning("{WorkerName}: Failed to deserialize message", _workerName);
                            await _channel.BasicRejectAsync(deliveryTag: eventArgs.DeliveryTag, requeue: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{WorkerName}: Error processing message", _workerName);

                        // Возвращаем в очередь для повторной обработки
                        await _channel.BasicNackAsync(
                            deliveryTag: eventArgs.DeliveryTag,
                            multiple: false,
                            requeue: true);
                    }
                };

                // Начинаем потребление
                _consumerTag = await _channel.BasicConsumeAsync(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("{WorkerName}: Started consuming from queue {QueueName}", _workerName, _queueName);

                // Ожидаем отмены
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("{WorkerName} was cancelled", _workerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{WorkerName}: Critical error", _workerName);
                throw;
            }
            finally
            {
                await StopConsumingAsync();
                _logger.LogInformation("{WorkerName} stopped", _workerName);
            }
        }

        /// <summary>
        /// Абстрактный метод для обработки сообщения.
        /// В случае ошибки следует выбросить исключение, которое будет залогировано базовым классом,
        /// и сообщение будет возвращено в очередь для повторной обработки.
        /// Для получения scoped зависимостей используйте _serviceScopeFactory.CreateScope().
        /// </summary>
        /// <param name="message">Сообщение для обработки.</param>
        protected abstract Task ProcessMessageAsync(TMessage message);

        private async Task StopConsumingAsync()
        {
            if (_channel != null && !string.IsNullOrEmpty(_consumerTag))
            {
                try
                {
                    await _channel.BasicCancelAsync(_consumerTag);
                    await _channel.CloseAsync();
                    _channel.Dispose();
                    _logger.LogInformation("{WorkerName}: Stopped consuming from queue {QueueName}", _workerName, _queueName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{WorkerName}: Error stopping consumer", _workerName);
                }
            }
        }
    }
}
