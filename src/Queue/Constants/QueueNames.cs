using Abstractions.Models.Enums;

namespace Queue.Constants
{
    public static class QueueNames
    {
        public const string MessageStatusUpdates = "message-status-updates";

        public static string GetChannelQueueName(ChannelType channel)
            => $"messages.{channel}";
    }
}
