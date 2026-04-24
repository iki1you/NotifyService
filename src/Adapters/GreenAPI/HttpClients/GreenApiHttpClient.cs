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
                var errorMessage =
                    $"Error during API request: unexpected status code {(int)response.StatusCode} ({response.StatusCode}). Response: {responseBody}";

                if (IsTransientStatusCode(response.StatusCode))
                {
                    logger.LogWarning(errorMessage);
                    throw new TransientProviderException(errorMessage);
                }

                var error = response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => Error.BadRequest(errorMessage),
                    HttpStatusCode.Unauthorized => Error.Unauthorized(errorMessage),
                    HttpStatusCode.Forbidden => Error.Forbidden(errorMessage),
                    HttpStatusCode.TooManyRequests => Error.TooManyRequests(errorMessage),
                    _ => Error.BadRequest(errorMessage)
                };

                logger.LogError(error.Message);
                return error;
            }

            return responseBody;
        }
        catch (TaskCanceledException ex)
        {
            var errorMessage = $"Error during API request: timeout. {ex.Message}";
            logger.LogWarning(errorMessage);
            throw new TransientProviderException(errorMessage, ex);
        }
        catch (HttpRequestException ex)
        {
            var errorMessage = $"Error during API request: {ex.Message}";
            logger.LogWarning(errorMessage);
            throw new TransientProviderException(errorMessage, ex);
        }
        catch (TransientProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var error = Error.BadRequest($"Error: {ex.Message}");
            logger.LogError(error.Message);
            return error;
        }
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var status = (int)statusCode;
        return statusCode == HttpStatusCode.TooManyRequests || status >= 500;
    }
}
