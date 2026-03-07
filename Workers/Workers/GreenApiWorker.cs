using Abstractions.Models;
using Abstractions.Models.Enums;
using Adapters.GreenAPI.Models.Requests;
using Adapters.Interfaces;
using Queue.AbstractWorkers;

namespace Workers.Workers
{
    public class GreenApiWorker : MultiConsumerWorker
    {
        private readonly ILogger<GreenApiWorker> _logger;

        public GreenApiWorker(
            ILogger<GreenApiWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
            : base(logger, serviceScopeFactory, AdapterType.GreenAPI)
        {
            _logger = logger;
        }

        protected override async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            _logger.LogInformation("GreenAPI Worker: Processing message task {TaskId} for recipient {Recipient}",
                messageTask.Id, messageTask.Recipient);

            using var scope = _serviceScopeFactory.CreateScope();
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
                    RequestId = Guid.NewGuid(),
                    Status = MessageTaskStatus.Failed,
                    ErrorMessage = result.Error?.Message,
                    StatusChangedAt = DateTime.UtcNow
                });

                throw new InvalidOperationException($"Failed to send message: {result.Error?.Message}");
            }

            await PublishStatusAsync(new MessageTaskStatusDTO
            {
                MessageTaskId = messageTask.Id,
                RequestId = Guid.NewGuid(),
                Status = MessageTaskStatus.Sent,
                StatusChangedAt = DateTime.UtcNow
            });

            _logger.LogInformation("GreenAPI Worker: Message task {TaskId} sent successfully", messageTask.Id);
        }
    }
}
