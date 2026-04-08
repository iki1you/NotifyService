using Abstractions.Models;
using Abstractions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Entities
{
    [Index(nameof(RequestId))]
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
        public ChannelType Channel { get; set; }

        [Required]
        [MaxLength(500)]
        public string Recipient { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public MessageTaskStatus Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
