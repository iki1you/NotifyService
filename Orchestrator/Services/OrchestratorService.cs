using Abstractions.Models;
using Abstractions.Models.QueueEntities;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Data.Interfaces;
using Orchestrator.Interfaces;
using Orchestrator.Models;
using Queue.Interfaces;

namespace Orchestrator.Services
{
    public class Orchestrator : IOrchestratorService
    {
        private readonly IMessageRepository _messageRepo;
        private readonly ICredentialService _credentialService;
        private readonly IQueuePublisher _publisher;

        public Orchestrator(IMessageRepository messageRepo, ICredentialService routingService, IQueuePublisher publisher)
        {
            _messageRepo = messageRepo;
            _credentialService = routingService;
            _publisher = publisher;
        }

        public async Task<OperationResult<SendMessageResponse>> ProcessSendRequestAsync(SendMessageRequest request, long projectId)
        {
            if (await _messageRepo.RequestExistsAsync(request.RequestId))
            {
                var existingRequest = await _messageRepo.GetMessageRequestAsync(request.RequestId);
                if (existingRequest != null)
                {
                    var existingResponse = new SendMessageResponse
                    {
                        RequestId = request.RequestId,
                        StatusUrl = $"/api/status/{request.RequestId}"
                    };
                    return OperationResult<SendMessageResponse>.Success(existingResponse);
                }
                return OperationResult<SendMessageResponse>.Failure(new Error(StatusCode.BadRequest, "duplicate"));
            }

            var messageRequest = new MessageRequest
            {
                RequestId = request.RequestId,
                ProjectId = projectId,
                Status = "Processing",
                TotalRecipients = request.RecipientItems.Count,
                CreatedAt = DateTime.UtcNow
            };

            await _messageRepo.AddMessageRequestAsync(messageRequest);

            var channelErrors = new List<ChannelError>();

            foreach (var recipient in request.RecipientItems)
            {
                try
                {
                    var credential = await _credentialService.SelectCredentialAsync(projectId, recipient.Channel);

                    if (credential == null)
                    {
                        channelErrors.Add(new ChannelError
                        {
                            Channel = recipient.Channel,
                            Recipient = recipient.Recipient,
                            ErrorMessage = "No credentials found for the specified channel"
                        });
                        continue;
                    }

                    var task = new MessageTask
                    {
                        RequestId = request.RequestId,
                        ProjectId = projectId,
                        CredentialId = credential.CredentialId,
                        Content = request.Message.Content,
                        Recipient = recipient.Recipient,
                        Channel = recipient.Channel,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _messageRepo.AddMessageTaskAsync(task);

                    var queueName = $"{credential.AdapterType}.{credential.CredentialId}";
                    await _publisher.PublishAsync(queueName, task);
                }
                catch (Exception ex)
                {
                    channelErrors.Add(new ChannelError
                    {
                        Channel = recipient.Channel,
                        Recipient = recipient.Recipient,
                        ErrorMessage = ex.Message
                    });
                }
            }

            var response = new SendMessageResponse
            {
                RequestId = request.RequestId,
                StatusUrl = $"/api/status/{request.RequestId}",
                ChannelErrors = channelErrors
            };

            return OperationResult<SendMessageResponse>.Success(response);
        }
    }
}
