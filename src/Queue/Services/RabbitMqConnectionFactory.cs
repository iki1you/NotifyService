using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Queue.Configuration;
using Queue.Constants;
using RabbitMQ.Client;

namespace Queue.Services
{
    public interface IRabbitMqConnectionFactory
    {
        IConnection GetConnection();
    }

    public class RabbitMqConnectionFactory : IRabbitMqConnectionFactory, IHostedService, IAsyncDisposable
    {
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<RabbitMqConnectionFactory> _logger;
        private IConnection? _connection;
        private bool _started;

        public RabbitMqConnectionFactory(
            IOptions<RabbitMqSettings> settings,
            ILogger<RabbitMqConnectionFactory> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Creating RabbitMQ connection to {HostName}:{Port}",
                _settings.HostName, _settings.Port);

            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                Port = _settings.Port,
                UserName = _settings.UserName,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                RequestedHeartbeat = TimeSpan.FromSeconds(_settings.RequestedHeartbeat),
                NetworkRecoveryInterval = TimeSpan.FromSeconds(_settings.NetworkRecoveryInterval),
                AutomaticRecoveryEnabled = _settings.AutomaticRecoveryEnabled
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: QueueNames.RetryExchange,
                type: QueueNames.DelayedExchangeType,
                durable: true,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-delayed-type"] = ExchangeType.Direct
                },
                cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                exchange: QueueNames.DeadLetterExchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            foreach (var channelType in QueueNames.RetryManagedChannels)
            {
                var queueName = QueueNames.GetChannelQueueName(channelType);
                var dlqName = QueueNames.GetChannelDlqName(channelType);

                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: QueueNames.GetQueueArguments(queueName),
                    cancellationToken: cancellationToken);

                await channel.QueueDeclareAsync(
                    queue: dlqName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: cancellationToken);

                await channel.QueueBindAsync(
                    queue: dlqName,
                    exchange: QueueNames.DeadLetterExchange,
                    routingKey: queueName,
                    arguments: null,
                    cancellationToken: cancellationToken);

                await channel.QueueBindAsync(
                    queue: queueName,
                    exchange: QueueNames.RetryExchange,
                    routingKey: queueName,
                    arguments: null,
                    cancellationToken: cancellationToken);
            }

            _started = true;
            _logger.LogInformation("RabbitMQ connection created successfully");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!_started) return;
            _started = false;

            if (_connection != null)
            {
                _logger.LogInformation("Closing RabbitMQ connection gracefully...");
                await _connection.CloseAsync(cancellationToken: cancellationToken);
                _connection.Dispose();
                _connection = null;
            }
        }

        public IConnection GetConnection()
        {
            ObjectDisposedException.ThrowIf(_started, this);

            return _connection ?? throw new InvalidOperationException(
                "RabbitMQ connection is not initialized. Ensure the application has started.");
        }

        public async ValueTask DisposeAsync()
        {
            if (!_started)
            {
                await StopAsync(CancellationToken.None);
            }
            GC.SuppressFinalize(this);
        }
    }
}






