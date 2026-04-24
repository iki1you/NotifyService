using Adapters.Interfaces;
using Adapters.Services;
using Adapters.SMTP.Models;
using Adapters.SMTP.Models.Requests;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace Adapters.SMTP.Services
{
    public class SmtpSendService(
        ILogger<SmtpSendService> logger,
        CredentialService credentialService) : ISmtpSendService
    {
        public async Task<OperationResult> Send(ICollection<SmtpSendMessageRequest> requests, long credentialId)
        {
            var results = new List<OperationResult>();
            foreach (var request in requests)
            {
                results.Add(await Send(request, credentialId));
            }

            var firstFail = results.FirstOrDefault(x => x.IsFail);
            return firstFail ?? OperationResult.Success();
        }

        public async Task<OperationResult> Send(SmtpSendMessageRequest request, long credentialId)
        {
            var credentialResult = await credentialService.GetCredential<SmtpOptions>(credentialId);
            if (credentialResult.IsFail)
            {
                return credentialResult.Error!;
            }

            var options = credentialResult.Result!;
            var optionsValidationError = ValidateOptions(options);
            if (optionsValidationError is not null)
            {
                return optionsValidationError;
            }

            try
            {
                using var message = new MailMessage
                {
                    From = string.IsNullOrWhiteSpace(options.FromName)
                        ? new MailAddress(options.From)
                        : new MailAddress(options.From, options.FromName),
                    Subject = string.IsNullOrWhiteSpace(request.Title)
                        ? options.Subject ?? string.Empty
                        : request.Title,
                    Body = request.Content,
                    IsBodyHtml = options.IsBodyHtml
                };

                message.To.Add(request.Recipient);

                using var smtpClient = new SmtpClient(options.Host, options.Port)
                {
                    EnableSsl = options.EnableSsl,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = string.IsNullOrWhiteSpace(options.UserName)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(options.UserName, options.Password)
                };

                await smtpClient.SendMailAsync(message);
                return OperationResult.Success();
            }
            catch (SmtpException ex)
            {
                logger.LogError(ex, "SMTP send failed");
                return Error.BadGateway($"SMTP send failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected SMTP send error");
                return Error.BadRequest($"SMTP send failed: {ex.Message}");
            }
        }

        private static Error? ValidateOptions(SmtpOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.Host))
            {
                return Error.BadRequest("SMTP config validation failed: Host is required");
            }

            if (options.Port <= 0 || options.Port > 65535)
            {
                return Error.BadRequest("SMTP config validation failed: Port must be in range 1..65535");
            }

            if (string.IsNullOrWhiteSpace(options.From))
            {
                return Error.BadRequest("SMTP config validation failed: From is required");
            }

            return null;
        }
    }
}
