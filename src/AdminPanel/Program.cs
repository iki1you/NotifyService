using AdminPanel.Components;
using AdminPanel.Controllers;
using AdminPanel.Services;
using Data;
using Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shared.Logging;
using Shared.Logging.Extensions;

SerilogBootstrapper.ConfigureBootstrapLogger("NotifyService.AdminPanel");

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.AddSharedSerilog(
        builder.Configuration,
        "NotifyService.AdminPanel",
        builder.Environment.EnvironmentName);

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddAntDesign();
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = "NotifyService.Admin.Auth";
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
        });
    builder.Services.AddAuthorization();
    builder.Services.AddCascadingAuthenticationState();

    // Add DbContext
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContextFactory<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Add data services
    builder.Services.AddScoped<IDataService<MessageRequest>, MessageRequestService>();
    builder.Services.AddScoped<IDataService<MessageTask>, MessageTaskService>();
    builder.Services.AddScoped<IDataService<Credential>, CredentialService>();
    builder.Services.AddScoped<ApiTokenService>();

    var app = builder.Build();

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

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapAuthEndpoints();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .RequireAuthorization();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotifyService.AdminPanel terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
