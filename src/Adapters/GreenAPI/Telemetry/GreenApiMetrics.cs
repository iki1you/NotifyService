using System.Diagnostics.Metrics;

namespace Adapters.GreenAPI.Telemetry
{
    public static class GreenApiMetrics
    {
        public const string MeterName = "NotifyService.Adapters.GreenApi";

        private static readonly Meter Meter = new(MeterName);

        private static readonly Histogram<double> ExternalCallDurationMs =
            Meter.CreateHistogram<double>("external_call.duration", unit: "ms");

        private static readonly Counter<long> ExternalCallFailedTotal =
            Meter.CreateCounter<long>("external_call.failed.total");

        public static void RecordExternalCallDuration(double valueMs, string operation, string result)
        {
            ExternalCallDurationMs.Record(valueMs,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("result", result));
        }

        public static void IncrementExternalCallFailed(string operation)
        {
            ExternalCallFailedTotal.Add(1,
                new KeyValuePair<string, object?>("operation", operation));
        }
    }
}
