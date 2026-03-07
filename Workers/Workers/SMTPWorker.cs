using Abstractions.Models;
using Abstractions.Models.Enums;
using Queue.AbstractWorkers;

namespace Workers.Workers
{
    public class SMTPWorker : MultiConsumerWorker
    {
        private readonly ILogger<SMTPWorker> _logger;
        // TODO: Добавить ISMTPSendService когда он будет реализован
        // private readonly ISMTPSendService _smtpSendService;

        public SMTPWorker(
            ILogger<SMTPWorker> logger,
            IServiceScopeFactory serviceScopeFactory)
            // ISMTPSendService smtpSendService) // TODO: раскомментировать
            : base(logger, serviceScopeFactory, AdapterType.SMTP)
        {
            _logger = logger;
            // _smtpSendService = smtpSendService; // TODO: раскомментировать
        }

        protected override async Task ProcessMessageAsync(MessageTaskDTO messageTask)
        {
            _logger.LogInformation("SMTP Worker: Processing message task {TaskId} for recipient {Recipient}",
                messageTask.Id, messageTask.Recipient);

            // TODO: Реализовать отправку через SMTP
            // using var scope = _serviceScopeFactory.CreateScope();
            // var smtpSendService = scope.ServiceProvider.GetRequiredService<ISMTPSendService>();
            // var result = await smtpSendService.Send(...);
            // if (!result.IsSuccess)
            // {
            //     await PublishStatusAsync(new MessageTaskStatusDTO
            //     {
            //         MessageTaskId = messageTask.Id,
            //         RequestId = Guid.NewGuid(),
            //         Status = MessageTaskStatus.Failed,
            //         ErrorMessage = result.Error?.Message,
            //         StatusChangedAt = DateTime.UtcNow
            //     });
            //
            //     throw new InvalidOperationException($"Failed to send email: {result.Error?.Message}");
            // }
            //
            // await PublishStatusAsync(new MessageTaskStatusDTO
            // {
            //     MessageTaskId = messageTask.Id,
            //     RequestId = Guid.NewGuid(),
            //     Status = MessageTaskStatus.Sent,
            //     StatusChangedAt = DateTime.UtcNow
            // });

            await Task.CompletedTask;
            throw new NotImplementedException("SMTP worker is not implemented yet");
        }
    }
}
