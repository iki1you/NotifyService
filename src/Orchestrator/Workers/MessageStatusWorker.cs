using Abstractions.Models;
using Data.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Queue.AbstractWorkers;
using Queue.Constants;
using Queue.Services;
using Queue.Telemetry;
using System.Diagnostics;
using System.Globalization;

namespace Orchestrator.Workers
{
    /// <summary>
    /// Воркер для обработки статусов отправки сообщений.
    /// Принимает сообщения из очереди статусов и обновляет информацию в БД.
    /// </summary>
    public class MessageStatusWorker : SingleConsumerWorker<MessageTaskStatusDTO>
    {
        private readonly ILogger<MessageStatusWorker> _logger;

        public MessageStatusWorker(
            ILogger<MessageStatusWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory)
            : base(logger, serviceScopeFactory, connectionFactory, QueueNames.MessageStatusUpdates, "MessageStatusWorker")
        {
            _logger = logger;
        }

        protected override async Task ProcessMessageAsync(MessageTaskStatusDTO statusUpdate)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            using var persistActivity = QueueTelemetry.ActivitySource.StartActivity("db.insert_status", ActivityKind.Internal);
            persistActivity?.SetTag("message.task.id", statusUpdate.MessageTaskId);
            persistActivity?.SetTag("message.status", statusUpdate.Status.ToString());
            persistActivity?.SetTag("db.system", "postgresql");
            persistActivity?.SetTag("db.operation", "UPDATE");
            persistActivity?.SetTag("db.statement_summary", "UPDATE message_tasks SET status = ? WHERE id = ?");
            persistActivity?.SetTag("request.trace_id", statusUpdate.TraceId);

            // Получаем задачу из БД
            var messageTask = await messageRepository.GetMessageTaskByIdAsync(statusUpdate.MessageTaskId);

            if (messageTask == null)
            {
                throw new InvalidOperationException($"MessageTask {statusUpdate.MessageTaskId} not found in database");
            }

            // Обновляем статус
            messageTask.Status = statusUpdate.Status;
            var activityTraceId = Activity.Current?.TraceId.ToString();
            var effectiveTraceId = activityTraceId ?? statusUpdate.TraceId;

            var statusPersistStartedAt = DateTimeOffset.UtcNow;

            await messageRepository.UpdateMessageTaskAsync(messageTask);

            var statusPersistCompletedAt = DateTimeOffset.UtcNow;
            var statusPersistDurationMs = (statusPersistCompletedAt - statusPersistStartedAt).TotalMilliseconds;

            persistActivity?.SetTag("request.id", messageTask.RequestId);
            persistActivity?.SetTag("status.persist.started_at_utc", statusPersistStartedAt.ToString("O", CultureInfo.InvariantCulture));
            persistActivity?.SetTag("status.persist.completed_at_utc", statusPersistCompletedAt.ToString("O", CultureInfo.InvariantCulture));
            persistActivity?.SetTag("status.persist.duration.ms", statusPersistDurationMs);

            _logger.LogInformation(
                "db.insert_status completed for request {RequestId}, task {TaskId}: {DbInsertStatusMs} ms. TraceId={TraceId}",
                messageTask.RequestId,
                statusUpdate.MessageTaskId,
                statusPersistDurationMs,
                effectiveTraceId);

            var requestId = statusUpdate.RequestId != Guid.Empty ? statusUpdate.RequestId : messageTask.RequestId;
            var messageRequest = await messageRepository.GetMessageRequestAsync(requestId);

            if (messageRequest != null)
            {
                effectiveTraceId ??= messageRequest.TraceId;

                if (!string.IsNullOrWhiteSpace(effectiveTraceId) && messageRequest.TraceId != effectiveTraceId)
                {
                    messageRequest.TraceId = effectiveTraceId;
                    await messageRepository.UpdateMessageRequestAsync(messageRequest);
                }

                var elapsedMs = (DateTime.UtcNow - messageRequest.CreatedAt).TotalMilliseconds;
                _logger.LogInformation(
                    "Status {Status} persisted for request {RequestId}, task {TaskId}. End-to-end latency from API receive: {ElapsedMs} ms. TraceId={TraceId}",
                    statusUpdate.Status,
                    requestId,
                    statusUpdate.MessageTaskId,
                    elapsedMs,
                    effectiveTraceId);
            }
            else
            {
                _logger.LogWarning(
                    "Status {Status} persisted for task {TaskId}, but request {RequestId} was not found. TraceId={TraceId}",
                    statusUpdate.Status,
                    statusUpdate.MessageTaskId,
                    requestId,
                    effectiveTraceId);
            }
        }
    }
}
