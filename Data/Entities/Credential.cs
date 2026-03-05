using Abstractions.Models;
using Abstractions.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Data.Entities
{
    [Table("Credentials")]
    public class Credential : IEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public long ProjectId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Channel { get; set; } = string.Empty;

        [Required]
        public AdapterType AdapterType { get; set; }

        [Required]
        [Column(TypeName = "jsonb")]
        public JsonDocument Config { get; set; } = JsonDocument.Parse("{}");

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
