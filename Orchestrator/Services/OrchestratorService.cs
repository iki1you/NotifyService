using Abstractions.Models;
using Abstractions.Models.Enums;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Data.Entities;
using Data.Interfaces;
using Orchestrator.Interfaces;
using Orchestrator.Models;
using Queue.Constants;
using Queue.Interfaces;

namespace Orchestrator.Services
{
    public class OrchestratorService : IOrchestratorService
    {
        private readonly IMessageRepository _messageRepo;
        private readonly ICredentialService _credentialService;
        private readonly IQueuePublisher _publisher;

        public OrchestratorService(IMessageRepository messageRepo, ICredentialService routingService, IQueuePublisher publisher)
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
                            Channel = recipient.Channel.ToString(),
                            Recipient = recipient.Recipient,
                            ErrorMessage = "No credentials found for the specified channel"
                        });
                        continue;
                    }

                    var taskEntity = new MessageTask
                    {
                        RequestId = request.RequestId,
                        ProjectId = projectId,
                        CredentialId = credential.CredentialId,
                        Content = request.Message.Content,
                        Recipient = recipient.Recipient,
                        Channel = recipient.Channel,
                        Status = MessageTaskStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _messageRepo.AddMessageTaskAsync(taskEntity);

                    var taskDto = new MessageTaskDTO
                    {
                        Id = taskEntity.Id,
                        RequestId = taskEntity.RequestId,
                        ProjectId = taskEntity.ProjectId,
                        CredentialId = taskEntity.CredentialId,
                        Content = taskEntity.Content,
                        Recipient = taskEntity.Recipient,
                        Channel = taskEntity.Channel.ToString(),
                        Status = taskEntity.Status,
                        CreatedAt = taskEntity.CreatedAt
                    };

                    var queueName = QueueNames.GetChannelQueueName(recipient.Channel);
                    await _publisher.PublishAsync(queueName, taskDto);
                }
                catch (Exception ex)
                {
                    channelErrors.Add(new ChannelError
                    {
                        Channel = recipient.Channel.ToString(),
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
