using Abstractions.Models;
using Abstractions.Models.Enums;
using Data.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Queue.Constants;
using Queue.Interfaces;
using System.Collections.Concurrent;

namespace Queue.AbstractWorkers
{
    /// <summary>
    /// Абстрактный базовый класс для воркеров, управляющих несколькими консьюмерами.
    /// Каждый консьюмер работает с собственной очередью и может быть запущен/остановлен независимо.
    /// Поддерживает периодическую синхронизацию консьюмеров.
    /// </summary>
    public abstract class MultiConsumerWorker : BackgroundService
    {
        private readonly ILogger _logger;
        protected readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly string _workerName;
        private readonly AdapterType _adapterType;
        private readonly TimeSpan _syncInterval;

        private readonly ConcurrentDictionary<long, ConsumerInfo> _consumers = new();

        /// <summary>
        /// Инициализирует новый экземпляр воркера с поддержкой множественных консьюмеров.
        /// </summary>
        /// <param name="logger">Logger для логирования операций воркера.</param>
        /// <param name="serviceScopeFactory">Factory для создания scopes при работе с scoped зависимостями.</param>
        /// <param name="adapterType">Тип адаптера для формирования имени очереди и получения credentials.</param>
        /// <param name="syncInterval">Интервал синхронизации консьюмеров (по умолчанию 1 минута).</param>
        protected MultiConsumerWorker(
            ILogger logger,
            IServiceScopeFactory serviceScopeFactory,
            AdapterType adapterType,
            TimeSpan? syncInterval = null)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _adapterType = adapterType;
            _workerName = $"{adapterType} Worker";
            _syncInterval = syncInterval ?? TimeSpan.FromMinutes(1);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{WorkerName} starting...", _workerName);

            using var timer = new PeriodicTimer(_syncInterval);

            // Первый запуск сразу
            await SynchronizeConsumersAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                    await SynchronizeConsumersAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{WorkerName}: Error while synchronizing consumers", _workerName);
                }
            }

            _logger.LogInformation("{WorkerName} stopping...", _workerName);

            // Останавливаем всех потребителей
            var stopTasks = _consumers.Values.Select(info => StopConsumerAsync(info)).ToList();
            await Task.WhenAll(stopTasks);

            _logger.LogInformation("{WorkerName} stopped", _workerName);
        }

        /// <summary>
        /// Синхронизирует состояние консьюмеров с актуальными данными.
        /// Запускает новых консьюмеров и останавливает неактивных.
        /// </summary>
        private async Task SynchronizeConsumersAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var credentialRepository = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
            var activeIds = await credentialRepository.GetActiveCredentialIdsByAdapterTypeAsync(_adapterType, stoppingToken);

            // Запускаем новых потребителей
            foreach (var id in activeIds)
            {
                if (!_consumers.ContainsKey(id))
                {
                    await StartConsumerAsync(id, stoppingToken);
                }
            }

            // Останавливаем потребителей для неактивных элементов
            var idsToRemove = _consumers.Keys.Except(activeIds).ToList();
            foreach (var id in idsToRemove)
            {
                if (_consumers.TryRemove(id, out var consumerInfo))
                {
                    await StopConsumerAsync(consumerInfo);
                }
            }

            _logger.LogInformation("{WorkerName}: Active consumers: {Count}", _workerName, _consumers.Count);
        }

        /// <summary>
        /// Запускает нового консьюмера для указанного идентификатора.
        /// </summary>
        private async Task StartConsumerAsync(long id, CancellationToken stoppingToken)
        {
            var queueName = $"{_adapterType}.{id}";
            _logger.LogInformation("{WorkerName}: Starting consumer for queue: {QueueName}", _workerName, queueName);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var scope = _serviceScopeFactory.CreateScope();
            var queueConsumer = scope.ServiceProvider.GetRequiredService<IQueueConsumer>();

            var task = Task.Run(async () =>
            {
                try
                {
                    await queueConsumer.StartConsuming(queueName, HandleProcessMessageAsync, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("{WorkerName}: Consumer for {QueueName} was cancelled", _workerName, queueName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{WorkerName}: Consumer for {QueueName} failed", _workerName, queueName);
                }
                finally
                {
                    scope.Dispose();
                }
            }, stoppingToken);

            var consumerInfo = new ConsumerInfo
            {
                Id = id,
                QueueName = queueName,
                CancellationTokenSource = cts,
                Task = task,
                Scope = scope
            };

            if (!_consumers.TryAdd(id, consumerInfo))
            {
                cts.Cancel();
                cts.Dispose();
                scope.Dispose();
                _logger.LogWarning("{WorkerName}: Failed to add consumer for id {Id}", _workerName, id);
            }
        }

        /// <summary>
        /// Останавливает консьюмера и удаляет его очередь.
        /// </summary>
        private async Task StopConsumerAsync(ConsumerInfo info)
        {
            _logger.LogInformation("{WorkerName}: Stopping consumer for queue: {QueueName}", _workerName, info.QueueName);

            try
            {
                info.CancellationTokenSource.Cancel();
                await info.Task;

                // Удаляем очередь
                using var deleteScope = _serviceScopeFactory.CreateScope();
                var queueConsumer = deleteScope.ServiceProvider.GetRequiredService<IQueueConsumer>();
                await queueConsumer.DeleteQueue(info.QueueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{WorkerName}: Error stopping consumer for {QueueName}", _workerName, info.QueueName);
            }
            finally
            {
                info.CancellationTokenSource.Dispose();
                info.Scope.Dispose();
            }
        }

        /// <summary>
        /// Обрабатывает сообщение с обработкой ошибок.
        /// Логирует ошибки и публикует статус Failed в случае исключения.
        /// </summary>
        /// <param name="message">Сообщение для обработки.</param>
        private async Task HandleProcessMessageAsync(object message)
        {
            if (message is not MessageTaskDTO messageTask)
            {
                _logger.LogWarning("{WorkerName}: Received message of unexpected type: {Type}", _workerName, message?.GetType().Name ?? "null");
                return;
            }

            try
            {
                await ProcessMessageAsync(messageTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{WorkerName}: Error processing message task {TaskId} for recipient {Recipient}",
                    _workerName, messageTask.Id, messageTask.Recipient);

                await PublishStatusAsync(new MessageTaskStatusDTO
                {
                    MessageTaskId = messageTask.Id,
                    RequestId = Guid.NewGuid(),
                    Status = MessageTaskStatus.Failed,
                    ErrorMessage = ex.Message,
                    StatusChangedAt = DateTime.UtcNow
                });

                throw;
            }
        }

        /// <summary>
        /// Абстрактный метод для обработки сообщения.
        /// В случае ошибки следует выбросить исключение, которое будет залогировано базовым классом.
        /// Для получения scoped зависимостей используйте _serviceScopeFactory.CreateScope().
        /// </summary>
        /// <param name="messageTask">Задача сообщения для обработки.</param>
        protected abstract Task ProcessMessageAsync(MessageTaskDTO messageTask);

        /// <summary>
        /// Публикует статус обработки сообщения в очередь статусов.
        /// Не выбрасывает исключение при ошибке публикации.
        /// </summary>
        /// <param name="statusUpdate">Обновление статуса для публикации.</param>
        protected async Task PublishStatusAsync(MessageTaskStatusDTO statusUpdate)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var statusPublisher = scope.ServiceProvider.GetRequiredService<IQueuePublisher>();
            await statusPublisher.PublishAsync(QueueNames.MessageStatusUpdates, statusUpdate, throwOnError: false);
        }

        /// <summary>
        /// Информация о запущенном консьюмере.
        /// </summary>
        private class ConsumerInfo
        {
            public required long Id { get; init; }
            public required string QueueName { get; init; }
            public required CancellationTokenSource CancellationTokenSource { get; init; }
            public required Task Task { get; init; }
            public required IServiceScope Scope { get; init; }
        }
    }
}
