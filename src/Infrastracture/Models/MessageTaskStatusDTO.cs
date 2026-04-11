using Abstractions.Models.Enums;

namespace Abstractions.Models
{
    public class MessageTaskStatusDTO
    {
        public long MessageTaskId { get; set; }
        public Guid RequestId { get; set; }
        public string? TraceId { get; set; }
        public MessageTaskStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime StatusChangedAt { get; set; }
    }
}
