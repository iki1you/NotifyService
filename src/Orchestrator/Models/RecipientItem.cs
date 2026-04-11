using System.ComponentModel.DataAnnotations;
using Abstractions.Models.Enums;

namespace Orchestrator.Models
{
    public class RecipientItem
    {
        [Required]
        public ChannelType Channel { get; set; }

        [Required]
        public string Recipient { get; set; }
    }
}
