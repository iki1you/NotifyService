using Newtonsoft.Json;

namespace Adapters.GreenAPI.Models.Message;

public class FileMessage : BaseMessage
{
    [JsonProperty("urlFile")]
    public string UrlFile { get; set; }
    [JsonProperty("fileName")]
    public string FileName { get; set; }
    [JsonProperty("caption")]
    public string Caption { get; set; } = string.Empty;
}
