using Abstractions.Models;
using Abstractions.Models.Enums;
using Adapters.GreenAPI.Models.Requests;
using Adapters.Interfaces;
using Data.Interfaces;
using Microsoft.Extensions.Configuration;
using Queue.Constants;
using Queue.AbstractWorkers;
using Queue.Interfaces;
using Queue.Services;
using System.Diagnostics;

namespace Workers.Workers
{
    public class GreenApiWorker : SingleConsumerWorker<MessageTaskDTO>
    {
        private readonly ILogger<GreenApiWorker> _logger;

        public GreenApiWorker(
            ILogger<GreenApiWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory,
            IConfiguration configuration)
            : base(
                logger,
                serviceScopeFactory,
                connectionFactory,
                QueueNames.GetChannelQueueName(ChannelType.WhatsApp),
                "GreenAPI Worker",
                prefetchCount: configuration.GetValue("Workers:PrefetchCount", (ushort)30))
        {
            _logger = logger;
        }

        protected override async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            var traceId = Activity.Current?.TraceId.ToString() ?? messageTask.TraceId;

            _logger.LogInformation(
                "WhatsApp Worker: Processing message task {TaskId} for request {RequestId} and recipient {Recipient}",
                messageTask.Id,
                messageTask.RequestId,
                messageTask.Recipient);

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

                _logger.LogWarning(
                    "WhatsApp Worker: Credential with id {CredentialId} not found for task {TaskId}, request {RequestId}",
                    messageTask.CredentialId,
                    messageTask.Id,
                    messageTask.RequestId);
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
                    ErrorMessage = $"Adapter {credential.AdapterType} is not supported for channel {ChannelType.WhatsApp}",
                    StatusChangedAt = DateTime.UtcNow
                });

                _logger.LogWarning(
                    "WhatsApp Worker: Unsupported adapter {AdapterType} for task {TaskId}, request {RequestId}",
                    credential.AdapterType,
                    messageTask.Id,
                    messageTask.RequestId);
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

                _logger.LogError(
                    "WhatsApp Worker: Failed to send message for task {TaskId}, request {RequestId}. Error: {Error}",
                    messageTask.Id,
                    messageTask.RequestId,
                    result.Error?.Message);
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

            _logger.LogInformation(
                "WhatsApp Worker: Message task {TaskId} for request {RequestId} sent successfully",
                messageTask.Id,
                messageTask.RequestId);
        }

        private async Task PublishStatusAsync(MessageTaskStatusDTO statusUpdate)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var statusPublisher = scope.ServiceProvider.GetRequiredService<IQueuePublisher>();
            await statusPublisher.PublishAsync(QueueNames.MessageStatusUpdates, statusUpdate, throwOnError: false);
        }
    }
}
