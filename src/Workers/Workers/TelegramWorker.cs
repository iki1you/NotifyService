using Abstractions.Models;
using Abstractions.Models.Enums;
using Adapters.GreenAPI.Models.Requests;
using Adapters.Interfaces;
using Data.Interfaces;
using Microsoft.Extensions.Configuration;
using Queue.AbstractWorkers;
using Queue.Constants;
using Queue.Interfaces;
using Queue.Services;
using System.Diagnostics;

namespace Workers.Workers
{
    public class TelegramWorker : SingleConsumerWorker<MessageTaskDTO>
    {
        private readonly ILogger<TelegramWorker> _logger;

        public TelegramWorker(
            ILogger<TelegramWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory,
            IConfiguration configuration)
            : base(
                logger,
                serviceScopeFactory,
                connectionFactory,
                QueueNames.GetChannelQueueName(ChannelType.Telegram),
                "Telegram Worker",
                prefetchCount: configuration.GetValue("Workers:PrefetchCount", (ushort)30))
        {
            _logger = logger;
        }

        protected override async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            var traceId = Activity.Current?.TraceId.ToString() ?? messageTask.TraceId;

            _logger.LogInformation("Telegram Worker: Processing message task {TaskId} for recipient {Recipient}",
                messageTask.Id, messageTask.Recipient);

            using var scope = _serviceScopeFactory.CreateScope();
            var credentialRepository = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();

            var credential = await credentialRepository.GetByIdAsync(messageTask.CredentialId);

            if (credential == null)
            {
                await PublishStatusAsync(new MessageTaskStatusDTO
                {
                    MessageTaskId = messageTask.Id,
                    RequestId = messageTask.RequestId,
                    TraceId = traceId,
                    Status = MessageTaskStatus.Failed,
                    ErrorMessage = $"Credential with id {messageTask.CredentialId} not found",
                    StatusChangedAt = DateTime.UtcNow
                });

                _logger.LogWarning("Telegram Worker: Credential with id {CredentialId} not found for task {TaskId}",
                    messageTask.CredentialId, messageTask.Id);
                return;
            }

            if (credential.AdapterType != AdapterType.GreenAPI)
            {
                await PublishStatusAsync(new MessageTaskStatusDTO
                {
                    MessageTaskId = messageTask.Id,
                    RequestId = messageTask.RequestId,
                    TraceId = traceId,
                    Status = MessageTaskStatus.Failed,
                    ErrorMessage = $"Adapter {credential.AdapterType} is not supported for channel {ChannelType.Telegram}",
                    StatusChangedAt = DateTime.UtcNow
                });

                _logger.LogWarning(
                    "Telegram Worker: Unsupported adapter {AdapterType} for task {TaskId}",
                    credential.AdapterType,
                    messageTask.Id);
                return;
            }

            var greenApiSendService = scope.ServiceProvider.GetRequiredService<IGreenApiSendService>();

            var request = new GreenApiSendMessageRequest
            {
                Recipient = messageTask.Recipient,
                Title = string.Empty,
                Content = messageTask.Content,
                Attachments = []
            };

            var result = await greenApiSendService.Send(request, messageTask.CredentialId);

            if (!result.IsSuccess)
            {
                await PublishStatusAsync(new MessageTaskStatusDTO
                {
                    MessageTaskId = messageTask.Id,
                    RequestId = messageTask.RequestId,
                    TraceId = traceId,
                    Status = MessageTaskStatus.Failed,
                    ErrorMessage = result.Error?.Message,
                    StatusChangedAt = DateTime.UtcNow
                });

                _logger.LogError("Telegram Worker: Failed to send message for task {TaskId}. Error: {Error}",
                    messageTask.Id, result.Error?.Message);
                return;
            }

            await PublishStatusAsync(new MessageTaskStatusDTO
            {
                MessageTaskId = messageTask.Id,
                RequestId = messageTask.RequestId,
                TraceId = traceId,
                Status = MessageTaskStatus.Sent,
                StatusChangedAt = DateTime.UtcNow
            });

            _logger.LogInformation("Telegram Worker: Message task {TaskId} sent successfully", messageTask.Id);
        }

        private async Task PublishStatusAsync(MessageTaskStatusDTO statusUpdate)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var statusPublisher = scope.ServiceProvider.GetRequiredService<IQueuePublisher>();
            await statusPublisher.PublishAsync(QueueNames.MessageStatusUpdates, statusUpdate, throwOnError: false);
        }
    }
}
