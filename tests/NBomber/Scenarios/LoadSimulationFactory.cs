using NBomber.CSharp;
using NBomber.Contracts;

internal static class LoadSimulationFactory
{
    public static LoadSimulation[] Get(string testType)
    {
        return testType switch
        {
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
            _ => throw new InvalidOperationException($"Unsupported TEST_TYPE='{testType}'. Use smoke|load|stress|spike|soak")
        };
    }
}
