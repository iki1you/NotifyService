using System.Text.Json.Serialization;

namespace Adapters.GreenAPI.Models.Responses;

public class GreenApiSendResponse
{
    [JsonPropertyName("idMessage")]
    public string IdMessage { get; set; }
}
