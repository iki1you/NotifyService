namespace Adapters.SMTP.Models
{
    public class SmtpOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 25;
        public bool EnableSsl { get; set; } = true;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string? FromName { get; set; }
        public string? Subject { get; set; }
        public bool IsBodyHtml { get; set; }
    }
}
