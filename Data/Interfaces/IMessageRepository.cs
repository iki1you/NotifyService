using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Data.Entities;

namespace Data.Interfaces
{
    public interface IMessageRepository
    {
        Task<OperationResult> AddMessageTaskAsync(MessageTask message);
        Task<OperationResult> AddMessageRequestAsync(MessageRequest request);
        Task<bool> RequestExistsAsync(Guid requestId);
        Task<MessageRequest?> GetMessageRequestAsync(Guid requestId);
        Task<MessageTask?> GetMessageTaskByIdAsync(long id);
        Task<IEnumerable<MessageTask>> GetMessageTasksByRequestIdAsync(Guid requestId);
        Task UpdateMessageTaskAsync(MessageTask task);
        Task UpdateMessageRequestAsync(MessageRequest request);
    }
}
