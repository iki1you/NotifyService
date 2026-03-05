using Abstractions.Models;

namespace Queue.Interfaces
{
    public interface IQueueConsumer
    {
        Task StartConsuming(string queueName, Func<MessageTaskDTO, Task> messageHandler, CancellationToken cancellationToken);
        Task StopConsuming();
    }
}
