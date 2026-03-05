using Abstractions.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Table("MessageTasks")]
    public class MessageTask : IEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public Guid RequestId { get; set; }

        [Required]
        public long ProjectId { get; set; }

        [Required]
        public long CredentialId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Channel { get; set; }

        [Required]
        [MaxLength(500)]
        public string Recipient { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
