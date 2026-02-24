using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Orchestrator.Models;

namespace Orchestrator.Interfaces
{
    public interface IOrchestratorService
    {
        Task<OperationResult> ProcessSendRequestAsync(SendMessageRequest request, long projectId);
    }
}
