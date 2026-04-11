using Adapters.GreenAPI.Models.Message;
using Adapters.GreenAPI.Services;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
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
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var error = Error.BadRequest(
                    $"Error during API request: unexpected status code {(int)response.StatusCode} ({response.StatusCode}). Response: {responseBody}");
                logger.LogError(error.Message);
                return error;
            }

            return responseBody;
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
