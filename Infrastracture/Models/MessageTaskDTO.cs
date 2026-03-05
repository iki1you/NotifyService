namespace Abstractions.Models
{
    public class MessageTaskDTO
    {
        public long Id { get; set; }
        public Guid RequestId { get; set; }
        public long ProjectId { get; set; }
        public long CredentialId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string Recipient { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
