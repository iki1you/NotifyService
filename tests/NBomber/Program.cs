using NBomber.CSharp;

var settings = TestSettings.FromEnvironment();

await RateLimitVerification.PrepareAsync(settings);

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(settings.ApiUrl)
};

var scenario = NotificationScenarioFactory
    .Create(settings, httpClient)
    .WithLoadSimulations(LoadSimulationFactory.Get(settings));

NBomberRunner
    .RegisterScenarios(scenario)
    .Run();

await RateLimitVerification.VerifyAsync(settings);
