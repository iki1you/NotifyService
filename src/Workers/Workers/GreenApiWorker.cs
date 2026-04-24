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
        private readonly IRateLimiter _rateLimiter;

        public GreenApiWorker(
            ILogger<GreenApiWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory,
            IRateLimiter rateLimiter,
            IConfiguration configuration)
            : base(
                logger,
                serviceScopeFactory,
                connectionFactory,
                QueueNames.GetChannelQueueName(ChannelType.WhatsApp),
                nameof(GreenApiWorker),
                AdapterType.GreenAPI,
                SingleConsumerWorkerSettings.FromConfiguration(configuration))
        {
            _logger = logger;
            _rateLimiter = rateLimiter;
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
            var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            var existingTask = await messageRepository.GetMessageTaskByIdAsync(messageTask.Id);
            if (existingTask == null)
            {
                throw new InvalidOperationException($"Message task {messageTask.Id} not found for request {messageTask.RequestId}");
            }

            if (existingTask.Status == MessageTaskStatus.Sent)
            {
                _logger.LogInformation(
                    "WhatsApp Worker: Task {TaskId} for request {RequestId} already sent. Skipping duplicate delivery",
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

            if (credential.AdapterType != AdapterType.GreenAPI)
            {
                throw new InvalidOperationException(
                    $"Adapter {credential.AdapterType} is not supported for channel {ChannelType.WhatsApp} for task {messageTask.Id}, request {messageTask.RequestId}");
            }

            var greenApiSendService = scope.ServiceProvider.GetRequiredService<IGreenApiSendService>();

            await _rateLimiter.WaitAsync(ChannelType.WhatsApp, AdapterType.GreenAPI);

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
                throw new InvalidOperationException(
                    $"WhatsApp provider failed for task {messageTask.Id}, request {messageTask.RequestId}. Error: {result.Error?.Message}");
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
