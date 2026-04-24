using Queue.Constants;
using RabbitMQ.Client;

namespace Queue.Extensions
{
    public static class ChannelRetryPublishExtensions
    {
        public static async Task PublishRetryAsync(
            this IChannel channel,
            string routingKey,
            ReadOnlyMemory<byte> body,
            int retryCount,
            TimeSpan delay,
            IDictionary<string, object?>? headers = null,
            CancellationToken cancellationToken = default)
        {
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = CloneHeaders(headers)
            };

            properties.Headers ??= new Dictionary<string, object?>();
            properties.Headers["x-delay"] = (int)Math.Max(1, delay.TotalMilliseconds);
            properties.Headers[QueueNames.RetryCountHeader] = retryCount;

            await channel.BasicPublishAsync(
                exchange: QueueNames.RetryExchange,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }

        private static Dictionary<string, object?> CloneHeaders(IDictionary<string, object?>? source)
        {
            if (source == null || source.Count == 0)
            {
                return [];
            }

            return source.ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
