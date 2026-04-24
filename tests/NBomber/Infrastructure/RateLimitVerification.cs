using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

internal static class RateLimitVerification
{
    public static async Task PrepareAsync(TestSettings settings)
    {
        if (!settings.VerifyRateLimit)
        {
            return;
        }

        using var wireMockClient = new HttpClient
        {
            BaseAddress = new Uri(settings.WireMockUrl)
        };

        using var resetResponse = await wireMockClient.PostAsync("/__admin/requests/reset", content: null);
        resetResponse.EnsureSuccessStatusCode();
    }

    public static async Task VerifyAsync(TestSettings settings)
    {
        if (!settings.VerifyRateLimit)
        {
            return;
        }

        using var wireMockClient = new HttpClient
        {
            BaseAddress = new Uri(settings.WireMockUrl)
        };

        using var prometheusClient = new HttpClient
        {
            BaseAddress = new Uri(settings.PrometheusUrl)
        };

        var requests = await GetWireMockTotalRequestsAsync(wireMockClient);
        if (requests == 0)
        {
            throw new InvalidOperationException("WireMock did not receive any provider requests. Rate limit verification cannot continue.");
        }

        var testDurationSeconds = Math.Max(1d, LoadSimulationFactory.GetPlannedDurationSeconds(settings));

        var actualRps = requests / testDurationSeconds;
        var allowedRps = settings.MaxProviderRps * (1 + settings.AllowedRpsTolerancePercent / 100d);

        Console.WriteLine($"[nbomber-rate-limit] requests={requests} duration_sec={testDurationSeconds:F2} actual_rps={actualRps:F2} allowed_rps={allowedRps:F2}");

        if (actualRps > allowedRps)
        {
            throw new InvalidOperationException(
                $"Rate limit violated: actual RPS {actualRps:F2} exceeds allowed {allowedRps:F2} (max {settings.MaxProviderRps:F2}, tolerance {settings.AllowedRpsTolerancePercent:F2}%).");
        }

        if (settings.VerifyPrometheusMetrics)
        {
            await VerifyPrometheusMetricsAsync(prometheusClient);
        }
    }

    private static async Task<long> GetWireMockTotalRequestsAsync(HttpClient wireMockClient)
    {
        using var response = await wireMockClient.GetAsync("/__admin/requests/count");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WireMockCountResponse>();
        return payload?.Count ?? 0;
    }

    private static async Task VerifyPrometheusMetricsAsync(HttpClient prometheusClient)
    {
        await AssertPrometheusMetricExistsAsync(prometheusClient, "{__name__=~\"notify_rate_limit_exceeded.*\"}", "notify_rate_limit_exceeded");
        await AssertPrometheusMetricExistsAsync(prometheusClient, "{__name__=~\"notify_rate_limit_fallback.*\"}", "notify_rate_limit_fallback");
        await AssertPrometheusMetricExistsAsync(prometheusClient, "{__name__=~\"notify_rate_limit_wait_time.*\"}", "notify_rate_limit_wait_time");
    }

    private static async Task AssertPrometheusMetricExistsAsync(HttpClient prometheusClient, string promQlSelector, string metricAlias)
    {
        var query = Uri.EscapeDataString($"sum({promQlSelector})");
        using var response = await prometheusClient.GetAsync($"/api/v1/query?query={query}");
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);

        var status = doc.RootElement.GetProperty("status").GetString();
        if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Prometheus query failed for metric '{metricAlias}'.");
        }

        var results = doc.RootElement
            .GetProperty("data")
            .GetProperty("result");

        if (results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Prometheus metric '{metricAlias}' is missing.");
        }

        var value = results[0].GetProperty("value")[1].GetString();
        _ = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private sealed class WireMockCountResponse
    {
        public long Count { get; set; }
    }
}
