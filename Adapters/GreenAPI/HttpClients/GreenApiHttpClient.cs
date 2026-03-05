using Adapters.GreenAPI.Models.Message;
using Adapters.GreenAPI.Services;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace Adapters.GreenAPI.HttpClients;

public class GreenApiHttpClient(ILogger<GreenApiSendService> logger)
{
    private readonly HttpClient httpClient = new();

    public async Task<OperationResult<string>> SendPostRequestAsync(string url, BaseMessage payload)
    {
        var jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            var error = Error.BadRequest($"Error during API request: {ex.Message}");
            logger.LogError(error.Message);
            return error;
        }
        catch (Exception ex)
        {
            var error = Error.BadRequest($"Error: {ex.Message}");
            logger.LogError(error.Message);
            return error;
        }
    }
}
