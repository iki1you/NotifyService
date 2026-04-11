namespace Adapters.GreenAPI.Models.Requests;

public class GreenApiSendMessageRequest
{
    public string Recipient { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public ICollection<GreenApiSendAttachmentRequest> Attachments { get; set; } = [];
}
