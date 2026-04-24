using System.Diagnostics.Metrics;

namespace Queue.Telemetry
{
    public static class QueueWorkerMetrics
    {
        public const string MeterName = "NotifyService.QueueWorkers";

        private static readonly Meter Meter = new(MeterName);

        private static readonly Histogram<double> ProcessingDurationMs =
            Meter.CreateHistogram<double>("processing.duration", unit: "ms");

        private static readonly Histogram<double> QueueWaitDurationMs =
            Meter.CreateHistogram<double>("queue_wait.duration", unit: "ms");

        private static readonly Counter<long> NackTotal =
            Meter.CreateCounter<long>("nack.total");

        private static readonly Counter<long> RetryTotal =
            Meter.CreateCounter<long>("retries.total");

        private static readonly Counter<long> SuccessTotal =
            Meter.CreateCounter<long>("success.total");

        private static readonly Counter<long> FailedTotal =
            Meter.CreateCounter<long>("failed.total");

        private static readonly Counter<long> NotifyRetryAttemptsTotal =
            Meter.CreateCounter<long>("notify_retry_attempts_total");

        public static void RecordProcessingDuration(double valueMs, string workerName, string queueName, string messageType)
        {
            ProcessingDurationMs.Record(valueMs,
                new KeyValuePair<string, object?>("worker", workerName),
                new KeyValuePair<string, object?>("queue", queueName),
                new KeyValuePair<string, object?>("message_type", messageType));
        }

        public static void RecordQueueWaitDuration(double valueMs, string workerName, string queueName, string messageType)
        {
            QueueWaitDurationMs.Record(valueMs,
                new KeyValuePair<string, object?>("worker", workerName),
                new KeyValuePair<string, object?>("queue", queueName),
                new KeyValuePair<string, object?>("message_type", messageType));
        }

        public static void IncrementNack(string workerName, string queueName, string messageType)
        {
            NackTotal.Add(1,
                new KeyValuePair<string, object?>("worker", workerName),
                new KeyValuePair<string, object?>("queue", queueName),
                new KeyValuePair<string, object?>("message_type", messageType));
        }

        public static void IncrementRetry(string workerName, string queueName, string messageType)
        {
            RetryTotal.Add(1,
                new KeyValuePair<string, object?>("worker", workerName),
                new KeyValuePair<string, object?>("queue", queueName),
                new KeyValuePair<string, object?>("message_type", messageType));
        }

        public static void IncrementSuccess(string workerName, string queueName, string messageType)
        {
            SuccessTotal.Add(1,
                new KeyValuePair<string, object?>("worker", workerName),
                new KeyValuePair<string, object?>("queue", queueName),
                new KeyValuePair<string, object?>("message_type", messageType));
        }

        public static void IncrementFailed(string workerName, string queueName, string messageType)
        {
            FailedTotal.Add(1,
                new KeyValuePair<string, object?>("worker", workerName),
                new KeyValuePair<string, object?>("queue", queueName),
                new KeyValuePair<string, object?>("message_type", messageType));
        }

        public static void IncrementNotifyRetryAttempts(string channel, string adapter, string status)
        {
            NotifyRetryAttemptsTotal.Add(1,
                new KeyValuePair<string, object?>("channel", channel),
                new KeyValuePair<string, object?>("adapter", adapter),
                new KeyValuePair<string, object?>("status", status));
        }
    }
}
