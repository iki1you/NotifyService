namespace Queue.Interfaces
{
    public interface IQueuePublisher
    {
        Task PublishAsync<T>(string queueName, T message, bool throwOnError = true);
    }
}
