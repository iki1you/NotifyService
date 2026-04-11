using System.Globalization;

internal sealed record TestSettings(
    string TestType,
    string ApiUrl,
    string TestRunId,
    string? ApiBearerToken,
    int SlowMs,
    double ThinkMin,
    double ThinkMax)
{
    public static TestSettings FromEnvironment()
    {
        var testType = (Environment.GetEnvironmentVariable("TEST_TYPE") ?? "smoke").ToLowerInvariant();
        var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://notifyservice_api:8080";
        var testRunId = Environment.GetEnvironmentVariable("TEST_RUN_ID")
                        ?? Environment.GetEnvironmentVariable("TEST_RUN")
                        ?? $"run_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var apiBearerToken = Environment.GetEnvironmentVariable("API_BEARER_TOKEN")
                             ?? Environment.GetEnvironmentVariable("BEARER_TOKEN");

        return new TestSettings(
            testType,
            apiUrl,
            testRunId,
            apiBearerToken,
            ReadInt("SLOW_MS", 500),
            ReadDouble("THINK_TIME_MIN", 0.3d),
            ReadDouble("THINK_TIME_MAX", 1.2d));
    }

    private static int ReadInt(string envName, int defaultValue)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(envName), out var value) ? value : defaultValue;
    }

    private static double ReadDouble(string envName, double defaultValue)
    {
        var rawValue = Environment.GetEnvironmentVariable(envName);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return defaultValue;
        }

        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentCultureValue)
            ? currentCultureValue
            : defaultValue;
    }
}
