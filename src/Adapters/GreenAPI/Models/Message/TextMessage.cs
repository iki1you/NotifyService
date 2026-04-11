using Newtonsoft.Json;

namespace Adapters.GreenAPI.Models.Message;

public class TextMessage : BaseMessage
{
    [JsonProperty("message")]
    public string Message { get; set; }
}
