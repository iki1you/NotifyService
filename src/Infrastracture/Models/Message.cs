namespace Abstractions.Models
{
    public class Message : IEntity
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public ICollection<MessageAttachment> Attachments { get; set; } = [];
    }
}
