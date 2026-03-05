using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class CreateCredentialRequest
    {
        [Required]
        [MaxLength(50)]
        public string Channel { get; set; } = string.Empty;

        [Required]
        public long AdapterType { get; set; }

        [Required]
        public string Config { get; set; } = string.Empty;
    }
}
