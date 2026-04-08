using Abstractions.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Index(nameof(RequestId), IsUnique = true)]
    [Table("MessageRequests")]
    public class MessageRequest : IEntity
    {
        [Key]
        public long Id { get; set; }

        [Required]
        public Guid RequestId { get; set; }

        [Required]
        public long ProjectId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        [Required]
        public int TotalRecipients { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
