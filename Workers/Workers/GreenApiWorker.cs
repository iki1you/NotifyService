using Abstractions.Models;
using Abstractions.Models.Enums;
using Adapters.GreenAPI.Models.Requests;
using Adapters.Interfaces;

namespace Workers.Workers
{
    public class GreenApiWorker : BaseWorker
    {
        public GreenApiWorker(
            ILogger<GreenApiWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
            : base(logger, serviceScopeFactory, AdapterType.GreenAPI, "GreenAPI")
        {
        }

        protected override async Task ProcessMessageInternalAsync(MessageTaskDTO messageTask)
        {
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
                throw new InvalidOperationException($"Failed to send message: {result.Error?.Message}");
            }
        }
    }
}
