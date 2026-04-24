using Adapters.SMTP.Models.Requests;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;

namespace Adapters.Interfaces
{
    public interface ISmtpSendService
    {
        Task<OperationResult> Send(ICollection<SmtpSendMessageRequest> requests, long credentialId);
        Task<OperationResult> Send(SmtpSendMessageRequest request, long credentialId);
    }
}
