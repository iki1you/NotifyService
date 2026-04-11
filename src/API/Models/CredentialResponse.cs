using Abstractions.Models.Enums;
using System.Text.Json;

namespace API.Models
{
    public class CredentialResponse
    {
        public long Id { get; set; }
        public long ProjectId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public AdapterType AdapterType { get; set; }
        public JsonDocument Config { get; set; } = JsonDocument.Parse("{}");
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
