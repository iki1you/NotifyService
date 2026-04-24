using Abstractions.Models.Enums;

namespace Queue.Configuration
{
    public class RetryPolicyOptions
    {
        public RetryChannelPolicyOptions WhatsApp { get; set; } = new();
        public RetryChannelPolicyOptions Email { get; set; } = new();
        public RetryChannelPolicyOptions Telegram { get; set; } = new();
        public RetryChannelPolicyOptions MAX { get; set; } = new();

        public RetryChannelPolicyOptions GetByQueueName(string queueName)
        {
            if (string.Equals(queueName, $"messages.{ChannelType.WhatsApp}", StringComparison.Ordinal))
            {
                return WhatsApp;
            }

            if (string.Equals(queueName, $"messages.{ChannelType.Email}", StringComparison.Ordinal))
            {
                return Email;
            }

            if (string.Equals(queueName, $"messages.{ChannelType.Telegram}", StringComparison.Ordinal))
            {
                return Telegram;
            }

            if (string.Equals(queueName, $"messages.{ChannelType.MAX}", StringComparison.Ordinal))
            {
                return MAX;
            }

            return new RetryChannelPolicyOptions();
        }
    }

    public class RetryChannelPolicyOptions
    {
        public int MaxRetries { get; set; } = 5;
        public int BaseDelaySeconds { get; set; } = 1;
        public int MaxDelaySeconds { get; set; } = 60;
    }
}
