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
using RateLimiter.Interfaces;
using System.Diagnostics;

namespace Workers.Workers
{
    public class TelegramWorker : SingleConsumerWorker<MessageTaskDTO>
    {
        private readonly ILogger<TelegramWorker> _logger;
        private readonly IRateLimiter _rateLimiter;

        public TelegramWorker(
            ILogger<TelegramWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory,
            IRateLimiter rateLimiter,
            IConfiguration configuration)
            : base(
                logger,
                serviceScopeFactory,
                connectionFactory,
                QueueNames.GetChannelQueueName(ChannelType.Telegram),
                nameof(TelegramWorker),
                AdapterType.GreenAPI,
                SingleConsumerWorkerSettings.FromConfiguration(configuration))
        {
            _logger = logger;
            _rateLimiter = rateLimiter;
        }

        protected override async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            var traceId = Activity.Current?.TraceId.ToString() ?? messageTask.TraceId;

            _logger.LogInformation("Telegram Worker: Processing message task {TaskId} for recipient {Recipient}",
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
                    "Telegram Worker: Task {TaskId} for request {RequestId} already sent. Skipping duplicate delivery",
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
                    $"Adapter {credential.AdapterType} is not supported for channel {ChannelType.Telegram} for task {messageTask.Id}, request {messageTask.RequestId}");
            }

            var greenApiSendService = scope.ServiceProvider.GetRequiredService<IGreenApiSendService>();

            await _rateLimiter.WaitAsync(ChannelType.Telegram, AdapterType.TelegramAPI);

            await _rateLimiter.WaitAsync(
                ChannelType.Telegram,
                AdapterType.TelegramAPI,
                messageTask.CredentialId.ToString());

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
                    $"Telegram provider failed for task {messageTask.Id}, request {messageTask.RequestId}. Error: {result.Error?.Message}");
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
