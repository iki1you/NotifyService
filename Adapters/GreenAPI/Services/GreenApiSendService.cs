using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Abstractions.Helpers;
using Adapters.GreenAPI.Models.Message;
using Adapters.GreenAPI.Models.Requests;
using Adapters.GreenAPI.Models.Responses;
using Adapters.GreenAPI.HttpClients;
using Adapters.Services;
using Adapters.Interfaces;

namespace Adapters.GreenAPI.Services
{
    public class GreenApiSendService(ILogger<GreenApiSendService> logger, GreenApiHttpClient httpClient, CredentialService credentialService) : IGreenApiSendService
    {
        public async Task<OperationResult> Send(ICollection<GreenApiSendMessageRequest> requests, long credentialId)
        {
            var results = new List<OperationResult>();
            foreach (var request in requests)
            {
                results.Add(await Send(request, credentialId));
            }

            var firstFail = results.FirstOrDefault(x => x.IsFail);
            return firstFail ?? OperationResult.Success();
        }

        public async Task<OperationResult> Send(GreenApiSendMessageRequest request, long credentialId)
        {
            var chatId = PrepareChatId(request.Recipient);
            var text = PrepareText(request.Title, request.Content);
            var textMessage = new TextMessage
            {
                ChatId = chatId,
                Message = text,
            };
            var textSendResult = await SendMessageAsync(textMessage, credentialId);

            if (textSendResult.IsFail)
            {
                return textSendResult.Error;
            }

            var files = request.Attachments.Select(a => new FileMessage
            {
                ChatId = chatId,
                FileName = PrepareFileName(a.FileName),
                UrlFile = PrepareFileUrl(a.PublicUrl)
            });

            var errorSendFiles = new Dictionary<FileMessage, OperationResult>();
            foreach (var file in files)
            {
                var sendFileResult = await SendFileByUrlAsync(file, credentialId);
                if (sendFileResult.IsFail)
                {
                    errorSendFiles.Add(file, sendFileResult);
                }
            }

            if (errorSendFiles.Count > 0)
            {
                return errorSendFiles.First().Value;
            }
            return OperationResult.Success();
        }

        public async Task<OperationResult<GreenApiSendResponse>> SendFileByUrlAsync(FileMessage message, long credentialId)
        {
            var credentialResult = await credentialService.GetCredential(credentialId);
            if (credentialResult.IsFail)
            {
                return credentialResult.Error;
            }
            var credential = credentialResult.Result;

            var url = $"{credential.ApiUrl}/waInstance{credential.IdInstance}/sendFileByUrl/{credential.ApiTokenInstance}";
            var sendResult = await httpClient.SendPostRequestAsync(url, message);

            if (sendResult.IsFail)
            {
                return sendResult.Error;
            }
            try
            {
                var response = JsonConvert.DeserializeObject<GreenApiSendResponse>(sendResult.Result);
                return response;
            }
            catch (JsonException ex)
            {
                var error = Error.BadRequest($"Error during JSON parsing: {ex.Message}");
                logger.LogError(error.Message);
                return error;
            }
        }

        public async Task<OperationResult<GreenApiSendResponse>> SendMessageAsync(TextMessage message, long credentialId)
        {
            var credentialResult = await credentialService.GetCredential(credentialId);
            if (credentialResult.IsFail)
            {
                return credentialResult.Error;
            }

            var credential = credentialResult.Result;

            var url = $"{credential.ApiUrl}/waInstance{credential.IdInstance}/sendMessage/{credential.ApiTokenInstance}";

            var sendResult = await httpClient.SendPostRequestAsync(url, message);

            if (sendResult.IsFail)
            {
                return sendResult.Error;
            }
            try
            {
                var response = JsonConvert.DeserializeObject<GreenApiSendResponse>(sendResult.Result);
                return response;
            }
            catch (JsonException ex)
            {
                var error = Error.BadRequest($"Error during JSON parsing: {ex.Message}");
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

        private static string PrepareChatId(string recipient)
        {
            var normalizeNumber = PhoneNumberHelper.ExtractLastTenDigitsOrThrow(recipient);
            return "7" + normalizeNumber + "@c.us";
        }

        private static string PrepareText(string title, string content)
        {
            return $"{title}\n{content}";
        }

        private static string PrepareFileName(string fileName)
        {
            return fileName.Replace(' ', '_').Replace('#', '_').Replace('%', '_');
        }

        private static string PrepareFileUrl(string fileUrl)
        {
            return fileUrl.Replace(" ", "%20");
        }
    }
}
