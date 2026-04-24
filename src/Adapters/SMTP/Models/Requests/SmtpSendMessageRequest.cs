namespace Adapters.SMTP.Models.Requests
{
    public class SmtpSendMessageRequest
    {
        public string Recipient { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
