using Abstractions.Models;

namespace Queue.Interfaces
{
    public interface IQueuePublisher
    {
        Task PublishAsync(string queueName, MessageTaskDTO messageTask);
    }
}
