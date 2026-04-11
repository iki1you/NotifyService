using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Adapters.GreenAPI.Models.Requests;

namespace Adapters.Interfaces
{
    public interface IGreenApiSendService
    {
        Task<OperationResult> Send(ICollection<GreenApiSendMessageRequest> requests, long credentialId);
        Task<OperationResult> Send(GreenApiSendMessageRequest request, long credentialId);
    }
}
