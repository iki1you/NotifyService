using Abstractions.Models;
using Abstractions.Models.QueueEntities;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Data.Interfaces;

namespace Data.Repositories
{
    public class MessageRepository : IMessageRepository
    {
        private readonly AppDbContext _context;
        private readonly IRepository<MessageRequest> _requestRepository;
        private readonly IRepository<MessageTask> _taskRepository;

        public MessageRepository(
            AppDbContext context,
            IRepository<MessageRequest> requestRepository,
            IRepository<MessageTask> taskRepository)
        {
            _context = context;
            _requestRepository = requestRepository;
            _taskRepository = taskRepository;
        }

        public async Task<OperationResult> AddMessageTaskAsync(MessageTask message)
        {
            try
            {
                await _taskRepository.AddAsync(message);
                await _taskRepository.SaveChangesAsync();
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(Error.Internal($"Failed to add message task: {ex.Message}"));
            }
        }

        public async Task<OperationResult> AddMessageRequestAsync(MessageRequest request)
        {
            try
            {
                await _requestRepository.AddAsync(request);
                await _requestRepository.SaveChangesAsync();
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure(Error.Internal($"Failed to add message request: {ex.Message}"));
            }
        }

        public async Task<bool> RequestExistsAsync(Guid requestId)
        {
            return await _requestRepository.ExistsAsync(r => r.RequestId == requestId);
        }

        public async Task<MessageRequest?> GetMessageRequestAsync(Guid requestId)
        {
            return await _requestRepository.FirstOrDefaultAsync(r => r.RequestId == requestId);
        }

        public async Task<MessageTask?> GetMessageTaskByIdAsync(long id)
        {
            return await _taskRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<MessageTask>> GetMessageTasksByRequestIdAsync(Guid requestId)
        {
            return await _taskRepository.FindAsync(t => t.RequestId == requestId);
        }

        public async Task UpdateMessageTaskAsync(MessageTask task)
        {
            task.UpdatedAt = DateTime.UtcNow;
            _taskRepository.Update(task);
            await _taskRepository.SaveChangesAsync();
        }

        public async Task UpdateMessageRequestAsync(MessageRequest request)
        {
            request.UpdatedAt = DateTime.UtcNow;
            _requestRepository.Update(request);
            await _requestRepository.SaveChangesAsync();
        }
    }
}
