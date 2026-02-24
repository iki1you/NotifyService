using System.ComponentModel.DataAnnotations;

namespace Orchestrator.Models
{
    public class RecipientItem
    {
        [Required]
        public string Channel { get; set; }

        [Required]
        public string Recipient { get; set; }
    }
}
