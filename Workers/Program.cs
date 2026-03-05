using Data.Extensions;
using Queue.Extensions;
using Workers.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDataServices(builder.Configuration);
builder.Services.AddRabbitMqServices(builder.Configuration);

builder.Services.AddHostedService<GreenApiWorker>();
builder.Services.AddHostedService<SMTPWorker>();
builder.Services.AddHostedService<DashaMailWorker>();

var host = builder.Build();
host.Run();
