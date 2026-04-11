namespace Orchestrator.Models
{
    public class SendMessageResponse
    {
        public Guid RequestId { get; set; }
        public string StatusUrl { get; set; }
        public List<ChannelError> ChannelErrors { get; set; } = new();
    }

    public class ChannelError
    {
        public string Channel { get; set; }
        public string Recipient { get; set; }
        public string ErrorMessage { get; set; }
    }
}
