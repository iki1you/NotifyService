namespace Queue.Configuration
{
    public class RabbitMqSettings
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string VirtualHost { get; set; } = "/";
        public int RequestedHeartbeat { get; set; } = 60;
        public int NetworkRecoveryInterval { get; set; } = 10;
        public bool AutomaticRecoveryEnabled { get; set; } = true;
    }
}
