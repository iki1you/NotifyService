using API.Extensions;
using Data.Extensions;
using Orchestrator.Extensions;
using Queue.Extensions;
using Serilog;
using Shared.Logging;
using Shared.Logging.Extensions;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text.Json;
using System.Text.Json.Serialization;

SerilogBootstrapper.ConfigureBootstrapLogger("NotifyService.API");

try
{
    var builder = WebApplication.CreateBuilder(args);
    var serviceName = "NotifyService.API";
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
                .AddAspNetCoreInstrumentation()
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
        });

    builder.Services.AddDataServices(builder.Configuration);
    builder.Services.AddRabbitMqServices(builder.Configuration);
    builder.Services.AddOrchestratorServices();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerConfiguration();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "NotifyService API v1");
            options.RoutePrefix = string.Empty;
            options.DocumentTitle = "NotifyService API";
        });
    }

    app.Use(async (context, next) =>
    {
        try
        {
            await next();
        }
        finally
        {
            var endpointValue = context.GetEndpoint() is RouteEndpoint routeEndpoint
                ? routeEndpoint.RoutePattern.RawText
                : context.GetEndpoint()?.DisplayName ?? "unknown";

            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("HTTP");

            logger.LogInformation(
                "HTTP request completed {method} {endpoint} -> {status_code}",
                context.Request.Method,
                endpointValue,
                context.Response.StatusCode);
        }
    });

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotifyService.API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


