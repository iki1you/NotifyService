using Abstractions.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace API.Models
{
    public class CreateCredentialRequest
    {
        [Required]
        public ChannelType Channel { get; set; }

        [Required]
        public AdapterType AdapterType { get; set; }

        [Required]
        public JsonDocument Config { get; set; } = JsonDocument.Parse("{}");
    }
}
