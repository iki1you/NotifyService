using Abstractions.Models.Enums;

namespace Queue.Constants
{
    public static class QueueNames
    {
        public const string MessageStatusUpdates = "message-status-updates";
        public const string RetryExchange = "retry.exchange";
        public const string DeadLetterExchange = "dlx.exchange";
        public const string DelayedExchangeType = "x-delayed-message";
        public const string RetryCountHeader = "x-retry-count";

        private static readonly HashSet<string> RetryManagedQueues =
        [
            GetChannelQueueName(ChannelType.WhatsApp),
            GetChannelQueueName(ChannelType.Email),
            GetChannelQueueName(ChannelType.Telegram),
            GetChannelQueueName(ChannelType.MAX)
        ];

        public static readonly ChannelType[] RetryManagedChannels =
        [
            ChannelType.WhatsApp,
            ChannelType.Email,
            ChannelType.Telegram,
            ChannelType.MAX
        ];

        public static string GetChannelQueueName(ChannelType channel)
            => $"messages.{channel}";

        public static string GetChannelDlqName(ChannelType channel)
            => $"dlq.{channel.ToString().ToLowerInvariant()}";

        public static bool IsRetryManagedQueue(string queueName)
            => RetryManagedQueues.Contains(queueName);

        public static IDictionary<string, object?>? GetQueueArguments(string queueName)
        {
            if (!IsRetryManagedQueue(queueName))
            {
                return null;
            }

            return new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queueName
            };
        }
    }
}
