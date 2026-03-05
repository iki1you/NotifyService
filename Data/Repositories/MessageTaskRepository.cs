using Data.Entities;

namespace Data.Repositories
{
    public class MessageTaskRepository : AbstractRepository<MessageTask>
    {
        public MessageTaskRepository(AppDbContext context) : base(context)
        {
        }

        protected override IQueryable<MessageTask> DefaultIncludes(IQueryable<MessageTask> query)
        {
            return query;
        }
    }
}
