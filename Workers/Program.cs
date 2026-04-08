using Adapters.Extensions;
using Data.Extensions;
using Queue.Extensions;
using Workers.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDataServices(builder.Configuration);
builder.Services.AddAdapterServices();
builder.Services.AddRabbitMqServices(builder.Configuration);

builder.Services.AddHostedService<GreenApiWorker>();
builder.Services.AddHostedService<MAXWorker>();
builder.Services.AddHostedService<TelegramWorker>();
builder.Services.AddHostedService<EmailWorker>();

var host = builder.Build();
host.Run();
