using Abstractions.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace API.Models
{
    public class CreateCredentialRequest
    {
        [Required]
        [MaxLength(50)]
        public string Channel { get; set; } = string.Empty;

        [Required]
        public AdapterType AdapterType { get; set; }

        [Required]
        public JsonDocument Config { get; set; } = JsonDocument.Parse("{}");
    }
}
