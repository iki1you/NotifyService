using Abstractions.Models;
using Abstractions.Models.Enums;
using Data.Interfaces;
using Queue.Interfaces;
using System.Collections.Concurrent;

namespace Workers.Workers
{
    /// <summary>
    /// Абстрактный базовый класс для воркеров адаптеров.
    /// Управляет lifecycle consumers для каждого credential, автоматически синхронизирует с БД.
    /// </summary>
    public abstract class BaseWorker : BackgroundService
    {
        private readonly ILogger _logger;
        protected readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly AdapterType _adapterType;
        private readonly string _adapterName;

        private readonly ConcurrentDictionary<long, ConsumerInfo> _consumers = new();

        /// <summary>
        /// Инициализирует новый экземпляр воркера адаптера.
        /// </summary>
        /// <param name="logger">Logger для логирования операций воркера.</param>
        /// <param name="serviceScopeFactory">Factory для создания scopes при работе с scoped зависимостями.</param>
        /// <param name="adapterType">Тип адаптера из enum AdapterType.</param>
        /// <param name="adapterName">Имя адаптера для логирования и формирования имен очередей.</param>
        protected BaseWorker(
            ILogger logger,
            IServiceScopeFactory serviceScopeFactory,
            AdapterType adapterType,
            string adapterName)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _adapterType = adapterType;
            _adapterName = adapterName;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{AdapterName} Worker starting...", _adapterName);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

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
                    _logger.LogError(ex, "{AdapterName} Worker: Error while synchronizing credentials", _adapterName);
                }
            }

            _logger.LogInformation("{AdapterName} Worker stopping...", _adapterName);

            // Останавливаем всех потребителей
            var stopTasks = _consumers.Values.Select(info => StopConsumerAsync(info)).ToList();
            await Task.WhenAll(stopTasks);

            _logger.LogInformation("{AdapterName} Worker stopped", _adapterName);
        }

        private async Task SynchronizeConsumersAsync(CancellationToken stoppingToken)
        {
            List<long> activeCredentialIds;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var credentialRepository = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
                activeCredentialIds = await credentialRepository.GetActiveCredentialIdsByAdapterTypeAsync(_adapterType, stoppingToken);
            }

            // Запускаем новых потребителей
            foreach (var credentialId in activeCredentialIds)
            {
                if (!_consumers.ContainsKey(credentialId))
                {
                    await StartConsumerAsync(credentialId, stoppingToken);
                }
            }

            // Останавливаем потребителей для неактивных credentials
            var credentialsToRemove = _consumers.Keys.Except(activeCredentialIds).ToList();
            foreach (var credentialId in credentialsToRemove)
            {
                if (_consumers.TryRemove(credentialId, out var consumerInfo))
                {
                    await StopConsumerAsync(consumerInfo);
                }
            }

            _logger.LogInformation("{AdapterName} Worker: Active consumers: {Count}", _adapterName, _consumers.Count);
        }

        private async Task StartConsumerAsync(long credentialId, CancellationToken stoppingToken)
        {
            var queueName = $"{_adapterName}.{credentialId}";
            _logger.LogInformation("{AdapterName} Worker: Starting consumer for queue: {QueueName}", _adapterName, queueName);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var scope = _serviceScopeFactory.CreateScope();
            var queueConsumer = scope.ServiceProvider.GetRequiredService<IQueueConsumer>();

            var task = Task.Run(async () =>
            {
                try
                {
                    await queueConsumer.StartConsuming(queueName, ProcessMessageAsync, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("{AdapterName} Worker: Consumer for {QueueName} was cancelled", _adapterName, queueName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{AdapterName} Worker: Consumer for {QueueName} failed", _adapterName, queueName);
                }
                finally
                {
                    scope.Dispose();
                }
            }, stoppingToken);

            var consumerInfo = new ConsumerInfo
            {
                CredentialId = credentialId,
                QueueName = queueName,
                CancellationTokenSource = cts,
                Task = task,
                Scope = scope
            };

            if (!_consumers.TryAdd(credentialId, consumerInfo))
            {
                cts.Cancel();
                cts.Dispose();
                scope.Dispose();
                _logger.LogWarning("{AdapterName} Worker: Failed to add consumer for credential {CredentialId}", _adapterName, credentialId);
            }
        }

        private async Task StopConsumerAsync(ConsumerInfo info)
        {
            _logger.LogInformation("{AdapterName} Worker: Stopping consumer for queue: {QueueName}", _adapterName, info.QueueName);

            try
            {
                info.CancellationTokenSource.Cancel();
                await info.Task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{AdapterName} Worker: Error stopping consumer for {QueueName}", _adapterName, info.QueueName);
            }
            finally
            {
                info.CancellationTokenSource.Dispose();
                info.Scope.Dispose();
            }
        }

        private async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            try
            {
                _logger.LogInformation("{AdapterName} Worker: Processing message task {TaskId} for recipient {Recipient}",
                    _adapterName, messageTask.Id, messageTask.Recipient);

                await ProcessMessageInternalAsync(messageTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{AdapterName} Worker: Error processing message task {TaskId} for recipient {Recipient}",
                    _adapterName, messageTask.Id, messageTask.Recipient);
            }
        }

        /// <summary>
        /// Абстрактный метод для обработки сообщения конкретным адаптером.
        /// В случае ошибки следует выбросить исключение, которое будет залогировано базовым классом.
        /// Для получения scoped зависимостей используйте _serviceScopeFactory.CreateScope().
        /// </summary>
        /// <param name="messageTask">Задача на отправку сообщения из очереди.</param>
        protected abstract Task ProcessMessageInternalAsync(MessageTaskDTO messageTask);

        private class ConsumerInfo
        {
            public required long CredentialId { get; init; }
            public required string QueueName { get; init; }
            public required CancellationTokenSource CancellationTokenSource { get; init; }
            public required Task Task { get; init; }
            public required IServiceScope Scope { get; init; }
        }
    }
}
