using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Queue.Configuration;
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
        private bool _disposed;

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

            _logger.LogInformation("RabbitMQ connection created successfully");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public IConnection GetConnection()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _connection ?? throw new InvalidOperationException(
                "RabbitMQ connection is not initialized. Ensure the application has started.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _disposed = true;

            if (_connection != null)
            {
                _logger.LogInformation("Disposing RabbitMQ connection");

                await _connection.CloseAsync();
                _connection.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}






