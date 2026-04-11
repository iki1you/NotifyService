using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using WireMock.Logging;

var port = Environment.GetEnvironmentVariable("WIREMOCK_PORT") ?? "8080";

var server = WireMockServer.Start(new WireMockServerSettings
{
    Urls = [$"http://0.0.0.0:{port}"],
    StartAdminInterface = true,
    ReadStaticMappings = false,
    Logger = new WireMockConsoleLogger()
});

server.Given(
        Request.Create()
            .WithPath(new WildcardMatcher("/waInstance*/sendMessage/*"))
            .UsingPost())
    .RespondWith(
        Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBodyAsJson(new { idMessage = "mock-send-message-id" }));

server.Given(
        Request.Create()
            .WithPath(new WildcardMatcher("/waInstance*/sendFileByUrl/*"))
            .UsingPost())
    .RespondWith(
        Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBodyAsJson(new { idMessage = "mock-send-file-id" }));

Console.WriteLine($"WireMock.NET started at http://0.0.0.0:{port}");
Console.WriteLine("WireMock.NET request logging is enabled");
await Task.Delay(Timeout.Infinite);
