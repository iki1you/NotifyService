using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Abstractions.Models.Enums;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Queue.Configuration;
using Queue.Constants;
using Queue.Extensions;
using Queue.Services;
using Queue.Telemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Queue.AbstractWorkers
{
    public sealed class SingleConsumerWorkerSettings
    {
        public SingleConsumerWorkerSettings(
            ushort prefetchCount = 1,
            int maxRetryAttempts = 5,
            int baseRetryDelayMs = 5_000,
            int maxRetryDelayMs = 300_000)
        {
            PrefetchCount = prefetchCount == 0 ? (ushort)1 : prefetchCount;
            MaxRetryAttempts = Math.Max(0, maxRetryAttempts);
            BaseRetryDelayMs = Math.Max(1, baseRetryDelayMs);
            MaxRetryDelayMs = Math.Max(BaseRetryDelayMs, maxRetryDelayMs);
        }

        public ushort PrefetchCount { get; }
        public int MaxRetryAttempts { get; }
        public int BaseRetryDelayMs { get; }
        public int MaxRetryDelayMs { get; }

        public static SingleConsumerWorkerSettings FromConfiguration(IConfiguration configuration)
            => new(
                prefetchCount: configuration.GetValue<ushort>("Workers:PrefetchCount", 1),
                maxRetryAttempts: configuration.GetValue("Workers:Retry:MaxAttempts", 5),
                baseRetryDelayMs: configuration.GetValue("Workers:Retry:BaseDelayMs", 5_000),
                maxRetryDelayMs: configuration.GetValue("Workers:Retry:MaxDelayMs", 300_000));
    }

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
        private readonly string _adapterTag;
        private readonly SingleConsumerWorkerSettings _settings;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly object _processingSync = new();
        private IChannel? _channel;
        private string? _consumerTag;
        private int _inFlightMessages;
        private bool _shutdownRequested;
        private TaskCompletionSource _drainCompletion = CreateCompletedDrainCompletion();

        /// <summary>
        /// Инициализирует новый экземпляр воркера для работы с одной очередью.
        /// </summary>
        /// <param name="logger">Logger для логирования операций воркера.</param>
        /// <param name="serviceScopeFactory">Factory для создания scopes при работе с scoped зависимостями.</param>
        /// <param name="connectionFactory">Factory для подключения к RabbitMQ.</param>
        /// <param name="queueName">Имя очереди для прослушивания.</param>
        /// <param name="workerName">Имя воркера для логирования.</param>
        /// <param name="adapterType">Тип адаптера для метрик ретраев.</param>
        /// <param name="settings">Параметры воркера (prefetch и retry).</param>
        protected SingleConsumerWorker(
            ILogger logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory,
            string queueName,
            string workerName,
            AdapterType adapterType,
            SingleConsumerWorkerSettings settings)
            : this(
                logger,
                serviceScopeFactory,
                connectionFactory,
                queueName,
                workerName,
                GetAdapterTag(adapterType),
                settings)
        {
        }

        /// <summary>
        /// Инициализирует новый экземпляр воркера для работы с одной очередью.
        /// </summary>
        /// <param name="logger">Logger для логирования операций воркера.</param>
        /// <param name="serviceScopeFactory">Factory для создания scopes при работе с scoped зависимостями.</param>
        /// <param name="connectionFactory">Factory для подключения к RabbitMQ.</param>
        /// <param name="queueName">Имя очереди для прослушивания.</param>
        /// <param name="workerName">Имя воркера для логирования.</param>
        /// <param name="adapterTag">Тег адаптера для метрик ретраев.</param>
        /// <param name="settings">Параметры воркера (prefetch и retry).</param>
        protected SingleConsumerWorker(
            ILogger logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory,
            string queueName,
            string workerName,
            string adapterTag,
            SingleConsumerWorkerSettings settings)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _connectionFactory = connectionFactory;
            _queueName = queueName;
            _workerName = workerName;
            _adapterTag = string.IsNullOrWhiteSpace(adapterTag) ? "unknown" : adapterTag;
            _settings = settings;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        private static string GetAdapterTag(AdapterType adapterType)
            => adapterType switch
            {
                AdapterType.GreenAPI => "green-api",
                AdapterType.TelegramAPI => "telegram-api",
                AdapterType.SMTP => "smtp",
                AdapterType.DashaMailApi => "dasha-mail-api",
                _ => adapterType.ToString().ToLowerInvariant()
            };

        private static TaskCompletionSource CreateCompletedDrainCompletion()
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            completion.TrySetResult();
            return completion;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("{WorkerName} starting...", _workerName);

            try
            {
                var connection = _connectionFactory.GetConnection();
                _channel = await connection.CreateChannelAsync();
                _channel.BasicReturnAsync += OnBasicReturnAsync;

                // Объявляем очередь
                await _channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: QueueNames.GetQueueArguments(_queueName));

                // Настройка prefetch
                await _channel.BasicQosAsync(
                    prefetchSize: 0,
                    prefetchCount: _settings.PrefetchCount,
                    global: false);

                // Создаем асинхронного потребителя
                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.ReceivedAsync += async (sender, eventArgs) =>
                {
                    if (!TryBeginMessageProcessing())
                    {
                        await _channel.BasicNackAsync(
                            deliveryTag: eventArgs.DeliveryTag,
                            multiple: false,
                            requeue: true);
                        return;
                    }

                    try
                    {
                        if (eventArgs.Redelivered)
                        {
                            QueueWorkerMetrics.IncrementRetry(_workerName, _queueName, typeof(TMessage).Name);
                        }

                        var hasParentContext = QueueTelemetry.TryExtractParentContext(
                            eventArgs.BasicProperties.Headers,
                            out var parentContext,
                            out var baggage);

                        using var activity = hasParentContext
                            ? QueueTelemetry.ActivitySource.StartActivity("worker.receive", ActivityKind.Consumer, parentContext)
                            : QueueTelemetry.ActivitySource.StartActivity("worker.receive", ActivityKind.Consumer);

                        QueueTelemetry.ApplyBaggage(activity, baggage);

                        if (hasParentContext)
                        {
                            activity?.SetTag("messaging.rabbitmq.trace_id", parentContext.TraceId.ToString());
                        }

                        activity?.SetTag("messaging.system", "rabbitmq");
                        activity?.SetTag("messaging.operation", "process");
                        activity?.SetTag("messaging.destination.name", _queueName);
                        activity?.SetTag("messaging.destination.kind", "queue");

                        var body = eventArgs.Body.ToArray();
                        var messageJson = Encoding.UTF8.GetString(body);

                        _logger.LogDebug("{WorkerName}: Received message: {Message}", _workerName, messageJson);

                        var message = JsonSerializer.Deserialize<TMessage>(messageJson, _jsonOptions);

                        if (message != null)
                        {
                            var requestId = TryGetRequestId(message);
                            var messageTraceId = TryGetTraceId(message);
                            if (!string.IsNullOrWhiteSpace(requestId))
                            {
                                activity?.SetTag("request.id", requestId);
                                activity?.AddBaggage("request.id", requestId);
                            }

                            if (!string.IsNullOrWhiteSpace(messageTraceId))
                            {
                                activity?.SetTag("message.trace_id", messageTraceId);
                                activity?.SetTag("client.trace_id", messageTraceId);
                            }

                            var activityTraceId = activity?.TraceId.ToString();
                            var effectiveTraceId = activityTraceId;

                            if (!string.IsNullOrWhiteSpace(activityTraceId))
                            {
                                activity?.SetTag("request.trace_id", activityTraceId);
                            }

                            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
                            {
                                ["worker"] = _workerName,
                                ["queue"] = _queueName,
                                ["requestId"] = requestId,
                                ["traceId"] = effectiveTraceId,
                                ["message_trace_id"] = messageTraceId,
                                ["worker_id"] = _workerName,
                                ["queue_name"] = _queueName,
                                ["message_type"] = typeof(TMessage).Name
                            });

                            if (TryGetEnqueuedAtUtc(message, out var enqueuedAtUtc))
                            {
                                var queueWaitMs = Math.Max(0, (DateTimeOffset.UtcNow - enqueuedAtUtc).TotalMilliseconds);
                                QueueWorkerMetrics.RecordQueueWaitDuration(queueWaitMs, _workerName, _queueName, typeof(TMessage).Name);
                                activity?.SetTag("message.queue.wait.ms", queueWaitMs);
                                _logger.LogInformation(
                                    "Queue lag for request {RequestId} in worker {WorkerName}: {QueueLagMs} ms. TraceId={TraceId}",
                                    requestId ?? "unknown",
                                    _workerName,
                                    queueWaitMs,
                                    effectiveTraceId);
                            }

                            var startedAtUtc = DateTimeOffset.UtcNow;

                            await ProcessMessageAsync(message);

                            var completedAtUtc = DateTimeOffset.UtcNow;
                            var processingMs = (completedAtUtc - startedAtUtc).TotalMilliseconds;
                            activity?.SetTag("message.process.started_at_utc", startedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                            activity?.SetTag("message.process.completed_at_utc", completedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                            activity?.SetTag("message.process.duration.ms", processingMs);

                            QueueWorkerMetrics.RecordProcessingDuration(processingMs, _workerName, _queueName, typeof(TMessage).Name);
                            QueueWorkerMetrics.IncrementSuccess(_workerName, _queueName, typeof(TMessage).Name);

                            // Подтверждаем обработку
                            await _channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);

                            _logger.LogDebug("{WorkerName}: Message processed successfully. RequestId={RequestId}, TraceId={TraceId}", _workerName, requestId, effectiveTraceId);
                            _logger.LogInformation("{WorkerName}: Message acked. RequestId={RequestId}, TraceId={TraceId}", _workerName, requestId, effectiveTraceId);
                        }
                        else
                        {
                            _logger.LogWarning("{WorkerName}: Failed to deserialize message", _workerName);
                            QueueWorkerMetrics.IncrementFailed(_workerName, _queueName, typeof(TMessage).Name);
                            await _channel.BasicRejectAsync(deliveryTag: eventArgs.DeliveryTag, requeue: false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "{WorkerName}: Error processing message", _workerName);
                        QueueWorkerMetrics.IncrementFailed(_workerName, _queueName, typeof(TMessage).Name);

                        try
                        {
                            var currentRetryAttempt = GetRetryAttempt(eventArgs.BasicProperties.Headers);
                            var retryPolicy = GetRetryPolicy();
                            var canScheduleRetry =
                                ex is TransientProviderException &&
                                QueueNames.IsRetryManagedQueue(_queueName) &&
                                currentRetryAttempt < retryPolicy.MaxRetries;

                            if (canScheduleRetry)
                            {
                                var nextRetryAttempt = currentRetryAttempt + 1;
                                var delayMs = CalculateRetryDelayMs(nextRetryAttempt, retryPolicy);

                                await PublishRetryAsync(eventArgs, nextRetryAttempt, delayMs);
                                await _channel.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false);

                                QueueWorkerMetrics.IncrementRetry(_workerName, _queueName, typeof(TMessage).Name);
                                QueueWorkerMetrics.IncrementNotifyRetryAttempts(GetChannelTag(_queueName), _adapterTag, "scheduled");

                                _logger.LogWarning(
                                    "{WorkerName}: Retry {RetryAttempt}/{MaxRetryAttempts} scheduled in {DelayMs} ms for queue {QueueName}",
                                    _workerName,
                                    nextRetryAttempt,
                                    retryPolicy.MaxRetries,
                                    delayMs,
                                    _queueName);

                                return;
                            }

                            if (ex is TransientProviderException)
                            {
                                QueueWorkerMetrics.IncrementNotifyRetryAttempts(GetChannelTag(_queueName), _adapterTag, "exhausted");
                            }

                            QueueWorkerMetrics.IncrementNack(_workerName, _queueName, typeof(TMessage).Name);

                            // Отправляем сообщение в DLQ через DLX
                            await _channel.BasicRejectAsync(
                                deliveryTag: eventArgs.DeliveryTag,
                                requeue: false);

                            _logger.LogError(
                                "{WorkerName}: Message moved to DLQ for queue {QueueName}. Retry limit reached ({MaxRetryAttempts})",
                                _workerName,
                                _queueName,
                                retryPolicy.MaxRetries);
                        }
                        catch (Exception retryEx)
                        {
                            _logger.LogError(retryEx, "{WorkerName}: Failed to schedule retry, requeueing message", _workerName);
                            QueueWorkerMetrics.IncrementNack(_workerName, _queueName, typeof(TMessage).Name);

                            await _channel.BasicNackAsync(
                                deliveryTag: eventArgs.DeliveryTag,
                                multiple: false,
                                requeue: true);
                        }
                    }
                    finally
                    {
                        CompleteMessageProcessing();
                    }
                };

                // Начинаем потребление
                _consumerTag = await _channel.BasicConsumeAsync(
                    queue: _queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("{WorkerName}: Started consuming from queue {QueueName}", _workerName, _queueName);

                using var cancellationRegistration = stoppingToken.Register(() =>
                {
                    _ = RequestGracefulShutdownAsync("stopping-token");
                });

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
                await RequestGracefulShutdownAsync("worker-finalize");
                await StopConsumingAsync();
                _logger.LogInformation("{WorkerName} stopped", _workerName);
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return base.StartAsync(cancellationToken);
        }

        /// <summary>
        /// Абстрактный метод для обработки сообщения.
        /// В случае ошибки следует выбросить исключение, которое будет залогировано базовым классом,
        /// и сообщение будет возвращено в очередь для повторной обработки.
        /// Для получения scoped зависимостей используйте _serviceScopeFactory.CreateScope().
        /// </summary>
        /// <param name="message">Сообщение для обработки.</param>
        protected abstract Task ProcessMessageAsync(TMessage message);

        private async Task RequestGracefulShutdownAsync(string reason)
        {
            Task completionToAwait;
            string? consumerTagToCancel;
            var shouldCancelConsumer = false;

            lock (_processingSync)
            {
                if (!_shutdownRequested)
                {
                    _shutdownRequested = true;
                    shouldCancelConsumer = true;
                }

                consumerTagToCancel = _consumerTag;
                completionToAwait = _drainCompletion.Task;
            }

            if (shouldCancelConsumer)
            {
                _logger.LogInformation(
                    "{WorkerName}: graceful shutdown requested ({Reason}). Cancelling consumer and waiting for in-flight messages",
                    _workerName,
                    reason);
            }

            if (shouldCancelConsumer && _channel != null && !string.IsNullOrEmpty(consumerTagToCancel))
            {
                try
                {
                    await _channel.BasicCancelAsync(consumerTagToCancel);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{WorkerName}: failed to cancel consumer tag {ConsumerTag}", _workerName, consumerTagToCancel);
                }
            }

            await completionToAwait;
            _logger.LogInformation("{WorkerName}: all in-flight messages completed", _workerName);
        }

        private bool TryBeginMessageProcessing()
        {
            lock (_processingSync)
            {
                if (_shutdownRequested)
                {
                    return false;
                }

                _inFlightMessages++;
                if (_inFlightMessages == 1)
                {
                    _drainCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                return true;
            }
        }

        private void CompleteMessageProcessing()
        {
            TaskCompletionSource? completionToSet = null;

            lock (_processingSync)
            {
                if (_inFlightMessages <= 0)
                {
                    return;
                }

                _inFlightMessages--;
                if (_inFlightMessages == 0)
                {
                    completionToSet = _drainCompletion;
                }
            }

            completionToSet?.TrySetResult();
        }

        private async Task StopConsumingAsync()
        {
            if (_channel != null && !string.IsNullOrEmpty(_consumerTag))
            {
                try
                {
                    _channel.BasicReturnAsync -= OnBasicReturnAsync;
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

        private static string? TryGetRequestId(TMessage message)
        {
            var requestIdProperty = typeof(TMessage).GetProperty("RequestId");
            var requestIdValue = requestIdProperty?.GetValue(message);

            return requestIdValue switch
            {
                Guid guid when guid != Guid.Empty => guid.ToString(),
                string text when !string.IsNullOrWhiteSpace(text) => text,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => null
            };
        }

        private static bool TryGetEnqueuedAtUtc(TMessage message, out DateTimeOffset enqueuedAtUtc)
        {
            var createdAtProperty = typeof(TMessage).GetProperty("CreatedAt")
                ?? typeof(TMessage).GetProperty("StatusChangedAt");

            var value = createdAtProperty?.GetValue(message);

            if (value is DateTimeOffset dto)
            {
                enqueuedAtUtc = dto.ToUniversalTime();
                return true;
            }

            if (value is DateTime dt)
            {
                enqueuedAtUtc = dt.Kind == DateTimeKind.Utc
                    ? new DateTimeOffset(dt)
                    : new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
                return true;
            }

            enqueuedAtUtc = default;
            return false;
        }

        private static string? TryGetTraceId(TMessage message)
        {
            var traceIdProperty = typeof(TMessage).GetProperty("TraceId");
            var traceIdValue = traceIdProperty?.GetValue(message);

            return traceIdValue switch
            {
                string text when !string.IsNullOrWhiteSpace(text) => text,
                _ => null
            };
        }

        private int CalculateRetryDelayMs(int retryAttempt, RetryChannelPolicyOptions retryPolicy)
        {
            var baseDelayMs = Math.Max(1, retryPolicy.BaseDelaySeconds) * 1000d;
            var maxDelayMs = Math.Max(baseDelayMs, retryPolicy.MaxDelaySeconds * 1000d);
            var delay = baseDelayMs * Math.Pow(2, Math.Max(0, retryAttempt - 1));
            return (int)Math.Min(maxDelayMs, delay);
        }

        private async Task PublishRetryAsync(BasicDeliverEventArgs eventArgs, int retryAttempt, int delayMs)
        {
            await _channel!.PublishRetryAsync(
                routingKey: _queueName,
                body: eventArgs.Body,
                retryCount: retryAttempt,
                delay: TimeSpan.FromMilliseconds(delayMs),
                headers: CloneHeaders(eventArgs.BasicProperties.Headers));
        }

        private static Dictionary<string, object?> CloneHeaders(IDictionary<string, object?>? source)
        {
            if (source == null || source.Count == 0)
            {
                return new Dictionary<string, object?>();
            }

            return source.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        private static int GetRetryAttempt(IDictionary<string, object?>? headers)
        {
            if (headers == null || !headers.TryGetValue(QueueNames.RetryCountHeader, out var value) || value == null)
            {
                return 0;
            }

            return value switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                short shortValue => shortValue,
                byte byteValue => byteValue,
                sbyte sbyteValue => sbyteValue,
                byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0
            };
        }

        private RetryChannelPolicyOptions GetRetryPolicy()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var retryPolicySnapshot = scope.ServiceProvider.GetService<IOptionsSnapshot<RetryPolicyOptions>>();
            return retryPolicySnapshot?.Value.GetByQueueName(_queueName)
                ?? new RetryChannelPolicyOptions
                {
                    MaxRetries = _settings.MaxRetryAttempts,
                    BaseDelaySeconds = Math.Max(1, _settings.BaseRetryDelayMs / 1000),
                    MaxDelaySeconds = Math.Max(1, _settings.MaxRetryDelayMs / 1000)
                };
        }

        private static string GetChannelTag(string queueName)
        {
            const string prefix = "messages.";

            var channel = queueName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? queueName[prefix.Length..]
                : queueName;

            return channel.ToLowerInvariant();
        }

        private Task OnBasicReturnAsync(object sender, BasicReturnEventArgs eventArgs)
        {
            _logger.LogError(
                "{WorkerName}: Retry message was returned by broker. ReplyCode={ReplyCode}, ReplyText={ReplyText}, Exchange={Exchange}, RoutingKey={RoutingKey}",
                _workerName,
                eventArgs.ReplyCode,
                eventArgs.ReplyText,
                eventArgs.Exchange,
                eventArgs.RoutingKey);

            return Task.CompletedTask;
        }
    }
}
