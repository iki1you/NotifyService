using NBomber.CSharp;

var settings = TestSettings.FromEnvironment();

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(settings.ApiUrl)
};

var scenario = NotificationScenarioFactory
    .Create(settings, httpClient)
    .WithLoadSimulations(LoadSimulationFactory.Get(settings.TestType));

NBomberRunner
    .RegisterScenarios(scenario)
    .Run();
