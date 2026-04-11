using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Queue.Telemetry
{
    public static class QueueTelemetry
    {
        public const string ActivitySourceName = "NotifyService.Queue";
        public const string TraceParentHeader = "traceparent";
        public const string TraceStateHeader = "tracestate";
        public const string BaggageHeader = "baggage";

        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        public static void InjectContextIntoHeaders(IDictionary<string, object?>? headers, Activity? activity)
        {
            if (headers == null || activity is not { Id: not null })
            {
                return;
            }

            headers[TraceParentHeader] = Encoding.UTF8.GetBytes(activity.Id);

            if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
            {
                headers[TraceStateHeader] = Encoding.UTF8.GetBytes(activity.TraceStateString);
            }

            var baggage = FormatBaggage(activity.Baggage);
            if (!string.IsNullOrWhiteSpace(baggage))
            {
                headers[BaggageHeader] = Encoding.UTF8.GetBytes(baggage);
            }
        }

        public static bool TryExtractParentContext(
            IDictionary<string, object?>? headers,
            out ActivityContext parentContext,
            out IEnumerable<KeyValuePair<string, string>> baggage)
        {
            var traceParent = ReadHeaderAsString(headers, TraceParentHeader);
            var traceState = ReadHeaderAsString(headers, TraceStateHeader);
            var baggageRaw = ReadHeaderAsString(headers, BaggageHeader);

            baggage = ParseBaggage(baggageRaw);

            return ActivityContext.TryParse(traceParent, traceState, isRemote: true, out parentContext);
        }

        public static void ApplyBaggage(Activity? activity, IEnumerable<KeyValuePair<string, string>> baggage)
        {
            if (activity == null)
            {
                return;
            }

            foreach (var item in baggage)
            {
                if (!string.IsNullOrWhiteSpace(item.Key) && item.Value is not null)
                {
                    activity.AddBaggage(item.Key, item.Value);
                }
            }
        }

        public static string? ReadHeaderAsString(IDictionary<string, object?>? headers, string key)
        {
            if (headers == null || !headers.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            return value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
                string text => text,
                _ => value.ToString()
            };
        }

        private static string? FormatBaggage(IEnumerable<KeyValuePair<string, string?>> baggage)
        {
            var items = baggage
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value is not null)
                .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}")
                .ToArray();

            return items.Length == 0 ? null : string.Join(',', items);
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseBaggage(string? baggage)
        {
            if (string.IsNullOrWhiteSpace(baggage))
            {
                return [];
            }

            var result = new List<KeyValuePair<string, string>>();
            var items = baggage.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var item in items)
            {
                var keyValue = item.Split('=', 2, StringSplitOptions.TrimEntries);
                if (keyValue.Length != 2)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(keyValue[0]);
                var value = Uri.UnescapeDataString(keyValue[1]);

                if (!string.IsNullOrWhiteSpace(key))
                {
                    result.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            return result;
        }
    }
}
