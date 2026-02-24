namespace Abstractions.Models
{
    public class Message
    {
        public string Recipient { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public ICollection<MessageAttachment> Attachments { get; set; } = [];
    }
}
