using NBomber.CSharp;
using NBomber.Contracts;

internal static class LoadSimulationFactory
{
    public static LoadSimulation[] Get(TestSettings settings)
    {
        return settings.TestType switch
        {
            "target-rps" =>
            [
                Simulation.Inject(rate: settings.TargetRps, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(settings.TargetDurationSeconds))
            ],
            "smoke" =>
            [
                Simulation.KeepConstant(copies: 5, during: TimeSpan.FromMinutes(1))
            ],
            "load" =>
            [
                Simulation.RampingConstant(copies: 100, during: TimeSpan.FromMinutes(2)),
                Simulation.KeepConstant(copies: 100, during: TimeSpan.FromMinutes(5)),
                Simulation.RampingConstant(copies: 0, during: TimeSpan.FromMinutes(2))
            ],
            "stress" =>
            [
                Simulation.RampingConstant(copies: 100, during: TimeSpan.FromMinutes(1)),
                Simulation.RampingConstant(copies: 200, during: TimeSpan.FromMinutes(1)),
                Simulation.KeepConstant(copies: 200, during: TimeSpan.FromMinutes(3)),
                Simulation.RampingConstant(copies: 0, during: TimeSpan.FromMinutes(1))
            ],
            "spike" =>
            [
                Simulation.KeepConstant(copies: 10, during: TimeSpan.FromSeconds(30)),
                Simulation.RampingConstant(copies: 200, during: TimeSpan.FromSeconds(15)),
                Simulation.KeepConstant(copies: 200, during: TimeSpan.FromMinutes(1)),
                Simulation.RampingConstant(copies: 10, during: TimeSpan.FromSeconds(15)),
                Simulation.KeepConstant(copies: 10, during: TimeSpan.FromMinutes(1)),
                Simulation.RampingConstant(copies: 0, during: TimeSpan.FromSeconds(30))
            ],
            "soak" =>
            [
                Simulation.KeepConstant(copies: 50, during: TimeSpan.FromMinutes(30))
            ],
            "rate-limit" =>
            [
                Simulation.RampingConstant(copies: 200, during: TimeSpan.FromSeconds(20)),
                Simulation.KeepConstant(copies: 200, during: TimeSpan.FromMinutes(2)),
                Simulation.RampingConstant(copies: 0, during: TimeSpan.FromSeconds(20))
            ],
            _ => throw new InvalidOperationException($"Unsupported TEST_TYPE='{settings.TestType}'. Use smoke|load|stress|spike|soak|rate-limit|target-rps")
        };
    }

    public static double GetPlannedDurationSeconds(TestSettings settings)
    {
        return settings.TestType switch
        {
            "target-rps" => settings.TargetDurationSeconds,
            "smoke" => TimeSpan.FromMinutes(1).TotalSeconds,
            "load" => TimeSpan.FromMinutes(9).TotalSeconds,
            "stress" => TimeSpan.FromMinutes(6).TotalSeconds,
            "spike" => TimeSpan.FromMinutes(3.5).TotalSeconds,
            "soak" => TimeSpan.FromMinutes(30).TotalSeconds,
            "rate-limit" => TimeSpan.FromSeconds(160).TotalSeconds,
            _ => 1d
        };
    }
}
