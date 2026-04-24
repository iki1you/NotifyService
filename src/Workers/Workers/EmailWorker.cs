using Abstractions.Models;
using Abstractions.Models.Enums;
using Adapters.Interfaces;
using Adapters.SMTP.Models.Requests;
using Data.Interfaces;
using Microsoft.Extensions.Configuration;
using Queue.AbstractWorkers;
using Queue.Constants;
using Queue.Interfaces;
using Queue.Services;
using System.Diagnostics;

namespace Workers.Workers
{
    public class EmailWorker : SingleConsumerWorker<MessageTaskDTO>
    {
        private readonly ILogger<EmailWorker> _logger;
        private readonly IRateLimiter _rateLimiter;

        public EmailWorker(
            ILogger<EmailWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory,
            IRateLimiter rateLimiter,
            IConfiguration configuration)
            : base(
                logger,
                serviceScopeFactory,
                connectionFactory,
                QueueNames.GetChannelQueueName(ChannelType.Email),
                nameof(EmailWorker),
                AdapterType.SMTP,
                SingleConsumerWorkerSettings.FromConfiguration(configuration))
        {
            _logger = logger;
            _rateLimiter = rateLimiter;
        }

        protected override async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            var traceId = Activity.Current?.TraceId.ToString() ?? messageTask.TraceId;

            _logger.LogInformation("Email Worker: Processing message task {TaskId} for recipient {Recipient}",
                messageTask.Id, messageTask.Recipient);

            using var scope = _serviceScopeFactory.CreateScope();
            var credentialRepository = scope.ServiceProvider.GetRequiredService<ICredentialRepository>();
            var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            var existingTask = await messageRepository.GetMessageTaskByIdAsync(messageTask.Id);
            if (existingTask == null)
            {
                throw new InvalidOperationException($"Message task {messageTask.Id} not found for request {messageTask.RequestId}");
            }

            if (existingTask.Status == MessageTaskStatus.Sent)
            {
                _logger.LogInformation(
                    "Email Worker: Task {TaskId} for request {RequestId} already sent. Skipping duplicate delivery",
                    messageTask.Id,
                    messageTask.RequestId);
                return;
            }

            var credential = await credentialRepository.GetByIdAsync(messageTask.CredentialId);

            if (credential == null)
            {
                throw new InvalidOperationException(
                    $"Credential with id {messageTask.CredentialId} not found for task {messageTask.Id}, request {messageTask.RequestId}");
            }

            if (credential.AdapterType != AdapterType.SMTP)
            {
                throw new InvalidOperationException(
                    $"Adapter {credential.AdapterType} is not supported for channel {ChannelType.Email} for task {messageTask.Id}, request {messageTask.RequestId}");
            }

            var smtpSendService = scope.ServiceProvider.GetRequiredService<ISmtpSendService>();

            await _rateLimiter.WaitAsync(ChannelType.Email, AdapterType.SMTP);

            var request = new SmtpSendMessageRequest
            {
                Recipient = messageTask.Recipient,
                Title = string.Empty,
                Content = messageTask.Content
            };

            var result = await smtpSendService.Send(request, messageTask.CredentialId);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"SMTP provider failed for task {messageTask.Id}, request {messageTask.RequestId}. Error: {result.Error?.Message}");
            }

            existingTask.Status = MessageTaskStatus.Sent;
            await messageRepository.UpdateMessageTaskAsync(existingTask);

            await PublishStatusAsync(new MessageTaskStatusDTO
            {
                MessageTaskId = messageTask.Id,
                RequestId = messageTask.RequestId,
                TraceId = traceId,
                Status = MessageTaskStatus.Sent,
                StatusChangedAt = DateTime.UtcNow
            });

            _logger.LogInformation("Email Worker: Message task {TaskId} sent successfully", messageTask.Id);
        }

        private async Task PublishStatusAsync(MessageTaskStatusDTO statusUpdate)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var statusPublisher = scope.ServiceProvider.GetRequiredService<IQueuePublisher>();
            await statusPublisher.PublishAsync(QueueNames.MessageStatusUpdates, statusUpdate, throwOnError: false);
        }
    }
}
