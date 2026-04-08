using Abstractions.Models;
using Abstractions.Models.Enums;
using Data.Interfaces;
using Queue.AbstractWorkers;
using Queue.Constants;
using Queue.Interfaces;
using Queue.Services;

namespace Workers.Workers
{
    public class EmailWorker : SingleConsumerWorker<MessageTaskDTO>
    {
        private readonly ILogger<EmailWorker> _logger;

        public EmailWorker(
            ILogger<EmailWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory)
            : base(
                logger,
                serviceScopeFactory,
                connectionFactory,
                QueueNames.GetChannelQueueName(ChannelType.Email),
                "Email Worker")
        {
            _logger = logger;
        }

        protected override async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            _logger.LogInformation("Email Worker: Processing message task {TaskId} for recipient {Recipient}",
                messageTask.Id, messageTask.Recipient);

            using var scope = _serviceScopeFactory.CreateScope();
            var credentialRepository = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
            var credential = await credentialRepository.GetByIdAsync(messageTask.CredentialId);

            if (credential == null)
            {
                await PublishStatusAsync(new MessageTaskStatusDTO
                {
                    MessageTaskId = messageTask.Id,
                    RequestId = Guid.NewGuid(),
                    Status = MessageTaskStatus.Failed,
                    ErrorMessage = $"Credential with id {messageTask.CredentialId} not found",
                    StatusChangedAt = DateTime.UtcNow
                });

                _logger.LogWarning("Email Worker: Credential with id {CredentialId} not found for task {TaskId}",
                    messageTask.CredentialId, messageTask.Id);
                return;
            }

            var errorMessage = credential.AdapterType switch
            {
                AdapterType.SMTP => "SMTP adapter is not implemented yet",
                AdapterType.DashaMailApi => "DashaMail adapter is not implemented yet",
                _ => $"Adapter {credential.AdapterType} is not supported for channel {ChannelType.Email}"
            };

            await PublishStatusAsync(new MessageTaskStatusDTO
            {
                MessageTaskId = messageTask.Id,
                RequestId = Guid.NewGuid(),
                Status = MessageTaskStatus.Failed,
                ErrorMessage = errorMessage,
                StatusChangedAt = DateTime.UtcNow
            });

            _logger.LogWarning("Email Worker: Failed to process task {TaskId}. Reason: {Reason}",
                messageTask.Id, errorMessage);
        }

        private async Task PublishStatusAsync(MessageTaskStatusDTO statusUpdate)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var statusPublisher = scope.ServiceProvider.GetRequiredService<IQueuePublisher>();
            await statusPublisher.PublishAsync(QueueNames.MessageStatusUpdates, statusUpdate, throwOnError: false);
        }
    }
}
