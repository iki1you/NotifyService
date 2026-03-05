using Newtonsoft.Json;

namespace Adapters.GreenAPI.Models.Message;

public abstract class BaseMessage
{
    [JsonProperty("chatId")]
    public string ChatId { get; set; }
}
