using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;
using Serilog.Sinks.OpenTelemetry;
using System.Diagnostics;

namespace Shared.Logging.Extensions;

public static class SerilogBuilderExtensions
{
    private static int _selfLogInitialized;

    public static ILoggingBuilder AddSharedSerilog(
        this ILoggingBuilder loggingBuilder,
        IConfiguration configuration,
        string applicationName,
        string environmentName)
    {
        var logger = ConfigureLogger(configuration, environmentName, applicationName);

        Log.Logger = logger;
        loggingBuilder.ClearProviders();
        loggingBuilder.Services.AddSingleton<ILoggerProvider>(_ => new SerilogLoggerBridgeProvider(logger));
        return loggingBuilder;
    }

    private static Serilog.ILogger ConfigureLogger(
        IConfiguration configuration,
        string environmentName,
        string applicationName)
    {
        var loggerConfiguration = new LoggerConfiguration();
        var minimumLevel = configuration["Serilog:MinimumLevel:Default"] ?? configuration["Logging:LogLevel:Default"];
        var appLabel = ToLabelValue(applicationName);
        var envLabel = ToLabelValue(environmentName);

        if (Enum.TryParse<LogEventLevel>(minimumLevel, true, out var parsedLevel))
        {
            loggerConfiguration.MinimumLevel.Is(parsedLevel);
        }
        else
        {
            loggerConfiguration.MinimumLevel.Information();
        }

        loggerConfiguration
            .Enrich.FromLogContext()
            .Enrich.With<LevelLabelEnricher>()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", applicationName)
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.WithProperty("app", appLabel)
            .Enrich.WithProperty("env", envLabel)
            .Enrich.WithProperty("provider", "serilog");

        var writeToSection = configuration.GetSection("Serilog:WriteTo");
        if (!writeToSection.Exists())
        {
            loggerConfiguration.WriteTo.Console();
            return loggerConfiguration.CreateLogger();
        }

        foreach (var sink in writeToSection.GetChildren())
        {
            var sinkName = sink["Name"];

            if (string.Equals(sinkName, "Console", StringComparison.OrdinalIgnoreCase))
            {
                loggerConfiguration.WriteTo.Console();
                continue;
            }

            if (string.Equals(sinkName, "GrafanaLoki", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sinkName, "LokiHttp", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureLokiSink(loggerConfiguration, sink, appLabel, envLabel);
                continue;
            }

            if (string.Equals(sinkName, "OpenTelemetry", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sinkName, "Otlp", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureOpenTelemetrySink(loggerConfiguration, configuration, sink, applicationName, environmentName);
                continue;
            }

            if (!string.Equals(sinkName, "File", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = sink["Args:path"] ?? "logs/log-.txt";
            var rollingIntervalString = sink["Args:rollingInterval"];
            var sharedString = sink["Args:shared"];
            var flushToDiskIntervalString = sink["Args:flushToDiskInterval"];

            var rollingInterval = Enum.TryParse<RollingInterval>(rollingIntervalString, true, out var parsedRolling)
                ? parsedRolling
                : RollingInterval.Day;
            var shared = bool.TryParse(sharedString, out var parsedShared) && parsedShared;
            var flushToDiskInterval = TimeSpan.TryParse(flushToDiskIntervalString, out var parsedFlush)
                ? parsedFlush
                : TimeSpan.FromSeconds(1);

            loggerConfiguration.WriteTo.File(
                path,
                rollingInterval: rollingInterval,
                shared: shared,
                flushToDiskInterval: flushToDiskInterval);
        }


        return loggerConfiguration.CreateLogger();
    }

    private static void ConfigureOpenTelemetrySink(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IConfigurationSection sink,
        string applicationName,
        string environmentName)
    {
        var endpoint = sink["Args:endpoint"]
            ?? sink["Args:url"]
            ?? sink["Args:uri"]
            ?? configuration["OpenTelemetry:Otlp:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        var protocolValue = sink["Args:protocol"];
        var protocol = Enum.TryParse<OtlpProtocol>(protocolValue, true, out var parsedProtocol)
            ? parsedProtocol
            : OtlpProtocol.Grpc;

        loggerConfiguration.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = endpoint;
            options.Protocol = protocol;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = applicationName,
                ["deployment.environment"] = environmentName
            };
        });
    }

    private static void ConfigureLokiSink(
        LoggerConfiguration loggerConfiguration,
        IConfigurationSection sink,
        string appLabel,
        string envLabel)
    {
        var endpoint = sink["Args:endpoint"] ?? sink["Args:uri"] ?? sink["Args:url"] ?? "http://localhost:3100";
        if (ShouldEnableSelfLog(sink["Args:emitEventFailure"]))
        {
            EnsureSelfLogEnabled();
        }

        var labels = new List<LokiLabel>
        {
            new() { Key = "app", Value = appLabel },
            new() { Key = "env", Value = envLabel },
            new() { Key = "provider", Value = "serilog" }
        };

        loggerConfiguration.WriteTo.GrafanaLoki(
            endpoint,
            labels: labels,
            propertiesAsLabels: ["provider", "level", "endpoint", "method", "status_code", "queue_name", "message_type", "worker_id", "message_id", "user_id"]);
    }

    private static string ToLabelValue(string value)
        => value.ToLowerInvariant().Replace('.', '-').Replace('_', '-');

    private static bool ShouldEnableSelfLog(string? emitEventFailure)
    {
        if (string.IsNullOrWhiteSpace(emitEventFailure))
        {
            return true;
        }

        var options = emitEventFailure
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return options.Any(option => string.Equals(option, "WriteToSelfLog", StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureSelfLogEnabled()
    {
        if (Interlocked.Exchange(ref _selfLogInitialized, 1) == 1)
        {
            return;
        }

        SelfLog.Enable(message => Console.Error.WriteLine($"[Serilog.SelfLog] {message}"));
    }
}

internal sealed class SerilogLoggerBridgeProvider(Serilog.ILogger logger) : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        => new SerilogLoggerBridge(logger.ForContext("SourceContext", categoryName), () => _scopeProvider);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public void Dispose()
    {
    }
}

internal sealed class SerilogLoggerBridge(
    Serilog.ILogger logger,
    Func<IExternalScopeProvider> scopeProviderFactory) : Microsoft.Extensions.Logging.ILogger
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => scopeProviderFactory().Push(state);

    public bool IsEnabled(LogLevel logLevel)
        => logger.IsEnabled(MapLevel(logLevel));

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var contextualLogger = eventId.Id == 0
            ? logger
            : logger.ForContext("EventId", eventId.Id);

        var traceId = Activity.Current?.TraceId.ToString();
        var requestId = Activity.Current?.GetBaggageItem("request.id");

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            contextualLogger = contextualLogger.ForContext("trace_id", traceId);
        }

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            contextualLogger = contextualLogger.ForContext("request_id", requestId);
        }

        scopeProviderFactory().ForEachScope((scope, _) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> structuredScope)
            {
                foreach (var (key, value) in structuredScope)
                {
                    if (!string.IsNullOrWhiteSpace(key) && value is not null)
                    {
                        contextualLogger = contextualLogger.ForContext(key, value, destructureObjects: true);
                    }
                }
            }
        }, state: (object?)null);

        contextualLogger.Write(MapLevel(logLevel), exception, "{Message}", message);
    }

    private static LogEventLevel MapLevel(LogLevel level)
        => level switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
}

internal sealed class LevelLabelEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("level", logEvent.Level.ToString().ToLowerInvariant()));
    }
}
