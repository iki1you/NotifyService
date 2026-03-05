using Abstractions.Models;

namespace Data.Repositories
{
    public class MessageRequestRepository : AbstractRepository<MessageRequest>
    {
        public MessageRequestRepository(AppDbContext context) : base(context)
        {
        }

        protected override IQueryable<MessageRequest> DefaultIncludes(IQueryable<MessageRequest> query)
        {
            return query;
        }
    }
}
