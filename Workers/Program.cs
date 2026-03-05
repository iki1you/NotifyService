using Adapters.Extensions;
using Data.Extensions;
using Queue.Extensions;
using Workers.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDataServices(builder.Configuration);
builder.Services.AddAdapterServices();
builder.Services.AddRabbitMqServices(builder.Configuration);

builder.Services.AddHostedService<GreenApiWorker>();
// TODO: Раскомментировать когда будут реализованы сервисы отправки
// builder.Services.AddHostedService<SMTPWorker>();
// builder.Services.AddHostedService<DashaMailWorker>();

var host = builder.Build();
host.Run();
