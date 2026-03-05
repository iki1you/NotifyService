using Abstractions.Models.QueueEntities;

namespace Queue.Interfaces
{
    public interface IQueuePublisher
    {
        Task PublishAsync(string queueName, MessageTask messageTask);
    }
}
