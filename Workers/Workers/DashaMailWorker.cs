using Abstractions.Models;
using Abstractions.Models.Enums;
using Queue.AbstractWorkers;

namespace Workers.Workers
{
    public class DashaMailWorker : MultiConsumerWorker
    {
        private readonly ILogger<DashaMailWorker> _logger;
        // TODO: Добавить IDashaMailSendService когда он будет реализован
        // private readonly IDashaMailSendService _dashaMailSendService;

        public DashaMailWorker(
            ILogger<DashaMailWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
            // IDashaMailSendService dashaMailSendService) // TODO: раскомментировать
            : base(logger, serviceScopeFactory, AdapterType.DashaMailApi)
        {
            _logger = logger;
            // _dashaMailSendService = dashaMailSendService; // TODO: раскомментировать
        }

        protected override async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            _logger.LogInformation("DashaMail Worker: Processing message task {TaskId} for recipient {Recipient}",
                messageTask.Id, messageTask.Recipient);

            await Task.CompletedTask;
            throw new NotImplementedException("DashaMail worker is not implemented yet");
        }
    }
}
