using Abstractions.Models;
using Abstractions.Models.Enums;

namespace Workers.Workers
{
    public class SMTPWorker : BaseWorker
    {
        // TODO: Добавить ISMTPSendService когда он будет реализован
        // private readonly ISMTPSendService _smtpSendService;

        public SMTPWorker(
            ILogger<SMTPWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
            // ISMTPSendService smtpSendService) // TODO: раскомментировать
            : base(logger, serviceScopeFactory, AdapterType.SMTP, "SMTP")
        {
            // _smtpSendService = smtpSendService; // TODO: раскомментировать
        }

        protected override async Task ProcessMessageInternalAsync(MessageTaskDTO messageTask)
        {
            // TODO: Реализовать отправку через SMTP
            // using var scope = _serviceScopeFactory.CreateScope();
            // var smtpSendService = scope.ServiceProvider.GetRequiredService<ISMTPSendService>();
            // var result = await smtpSendService.Send(...);
            // if (!result.IsSuccess)
            // {
            //     throw new InvalidOperationException($"Failed to send email: {result.Error?.Message}");
            // }

            await Task.CompletedTask;
            throw new NotImplementedException("SMTP worker is not implemented yet");
        }
    }
}
