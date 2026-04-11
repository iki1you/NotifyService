using Adapters.Extensions;
using Data.Extensions;
using Queue.Extensions;
using Serilog;
using Shared.Logging;
using Shared.Logging.Extensions;
using Queue.Telemetry;
using Adapters.GreenAPI.Telemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Workers.Workers;

SerilogBootstrapper.ConfigureBootstrapLogger("NotifyService.Workers");

try
{
    var builder = Host.CreateApplicationBuilder(args);
    var serviceName = "NotifyService.Workers";
    var otlpEndpoint = builder.Configuration["OpenTelemetry:Otlp:Endpoint"];

    builder.Logging.AddSharedSerilog(
        builder.Configuration,
        serviceName,
        builder.Environment.EnvironmentName);

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName))
        .WithTracing(tracing =>
        {
            tracing
                .AddHttpClientInstrumentation()
                .AddSource("NotifyService.Queue");

            tracing.AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.Grpc;

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }
            });
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddHttpClientInstrumentation()
                .AddMeter(QueueWorkerMetrics.MeterName)
                .AddMeter(GreenApiMetrics.MeterName);

            metrics.AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.Grpc;

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }
            });
        });

    builder.Services.AddDataServices(builder.Configuration);
    builder.Services.AddAdapterServices();
    builder.Services.AddRabbitMqServices(builder.Configuration);

    builder.Services.AddHostedService<GreenApiWorker>();
    builder.Services.AddHostedService<MAXWorker>();
    builder.Services.AddHostedService<TelegramWorker>();
    builder.Services.AddHostedService<EmailWorker>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotifyService.Workers terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
