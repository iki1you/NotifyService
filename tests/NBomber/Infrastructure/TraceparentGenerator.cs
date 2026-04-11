using System.Security.Cryptography;

internal static class TraceparentGenerator
{
    public static (string TraceId, string Traceparent) Generate()
    {
        var traceId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var spanId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var traceparent = $"00-{traceId}-{spanId}-01";

        return (traceId, traceparent);
    }
}
