using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.CSharp;

internal static class NotificationScenarioFactory
{
    public static ScenarioProps Create(TestSettings settings, HttpClient httpClient)
    {
        return Scenario.Create("notification_flow", async _ =>
        {
            var trace = TraceparentGenerator.Generate();
            var requestId = Guid.NewGuid();

            var payload = new
            {
                requestId,
                recipientItems = new[]
                {
                    new
                    {
                        channel = "whatsApp",
                        recipient = $"+77777777777"
                    },
                    new
                    {
                        channel = "MAX",
                        recipient = $"+77777777777"
                    },
                    new
                    {
                        channel = "Email",
                        recipient = $"+77777777777"
                    },
                    new
                    {
                        channel = "Telegram",
                        recipient = $"+77777777777"
                    }
                },
                message = new
                {
                    title = "NBomber test",
                    content = $"nbomber {settings.TestType} test message {DateTime.UtcNow:O}"
                }
            };

            var postRequest = new HttpRequestMessage(HttpMethod.Post, "/api/MessageSend")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(settings.ApiBearerToken))
            {
                postRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiBearerToken);
            }

            postRequest.Headers.TryAddWithoutValidation("traceparent", trace.Traceparent);

            var postSw = Stopwatch.StartNew();
            using var postResponse = await httpClient.SendAsync(postRequest);
            postSw.Stop();

            if (postSw.ElapsedMilliseconds > settings.SlowMs)
            {
                Console.WriteLine($"[nbomber-slow-trace] test_run_id={settings.TestRunId} test_type={settings.TestType} endpoint=POST_/api/MessageSend trace_id={trace.TraceId} duration_ms={postSw.ElapsedMilliseconds}");
            }

            if (postResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return Response.Fail(((int)postResponse.StatusCode).ToString(), "POST status is not 200", 0, 0);
            }

            await ThinkAsync(settings.ThinkMin, settings.ThinkMax);

            return Response.Ok();
        });
    }

    private static async Task ThinkAsync(double min, double max)
    {
        var delaySeconds = min + Random.Shared.NextDouble() * (max - min);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
    }
}
