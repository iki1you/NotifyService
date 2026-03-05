using Abstractions.Models;
using Abstractions.Models.Enums;

namespace Workers.Workers
{
    public class DashaMailWorker : BaseWorker
    {
        // TODO: Добавить IDashaMailSendService когда он будет реализован
        // private readonly IDashaMailSendService _dashaMailSendService;

        public DashaMailWorker(
            ILogger<DashaMailWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
            // IDashaMailSendService dashaMailSendService) // TODO: раскомментировать
            : base(logger, serviceScopeFactory, AdapterType.SMTP, "DashaMail") // TODO: Заменить на правильный AdapterType
        {
            // _dashaMailSendService = dashaMailSendService; // TODO: раскомментировать
        }

        protected override async Task ProcessMessageInternalAsync(MessageTaskDTO messageTask)
        {
            // TODO: Реализовать отправку через DashaMail
            // using var scope = _serviceScopeFactory.CreateScope();
            // var dashaMailSendService = scope.ServiceProvider.GetRequiredService<IDashaMailSendService>();
            // var result = await dashaMailSendService.Send(...);
            // if (!result.IsSuccess)
            // {
            //     throw new InvalidOperationException($"Failed to send via DashaMail: {result.Error?.Message}");
            // }

            await Task.CompletedTask;
            throw new NotImplementedException("DashaMail worker is not implemented yet");
        }
    }
}
