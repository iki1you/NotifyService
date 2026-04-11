using Abstractions.Models;
using System.ComponentModel.DataAnnotations;

namespace Orchestrator.Models
{
    public class SendMessageRequest
    {
        [Required]
        public Guid RequestId { get; set; }

        [Required]
        public List<RecipientItem> RecipientItems { get; set; }

        [Required]
        public Message Message { get; set; }

        public string? TraceId { get; set; }

        public DateTimeOffset? Delay { get; set; }
    }
}
