using Serilog;

namespace Shared.Logging;

public static class SerilogBootstrapper
{
    public static void ConfigureBootstrapLogger(string applicationName)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";
        var appLabel = applicationName.ToLowerInvariant().Replace('.', '-').Replace('_', '-');
        var envLabel = environmentName.ToLowerInvariant().Replace('.', '-').Replace('_', '-');

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.WithProperty("app", appLabel)
            .Enrich.WithProperty("env", envLabel)
            .Enrich.WithProperty("provider", "serilog")
            .WriteTo.Console()
            .CreateLogger();
    }
}
