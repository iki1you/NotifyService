using Workers.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<GreenApiWorker>();
builder.Services.AddHostedService<SMTPWorker>();
builder.Services.AddHostedService<DashaMailWorker>();

var host = builder.Build();
host.Run();
