namespace API.Models
{
    public class CredentialResponse
    {
        public long Id { get; set; }
        public long ProjectId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public long AdapterType { get; set; }
        public string Config { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
