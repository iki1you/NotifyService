using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                        QueueWorkerMetrics.IncrementNack(_workerName, _queueName, typeof(TMessage).Name);

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
    }
}
