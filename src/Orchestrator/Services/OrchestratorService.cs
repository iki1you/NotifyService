using Abstractions.Models;
using Abstractions.Models.Enums;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Data.Entities;
using Data.Interfaces;
using Microsoft.Extensions.Logging;
using Orchestrator.Interfaces;
using Orchestrator.Models;
using Queue.Constants;
using Queue.Interfaces;
using System.Diagnostics;

namespace Orchestrator.Services
{
    public class OrchestratorService : IOrchestratorService
    {
        private readonly IMessageRepository _messageRepo;
        private readonly ICredentialService _credentialService;
        private readonly IQueuePublisher _publisher;
        private readonly ILogger<OrchestratorService> _logger;

        public OrchestratorService(
            IMessageRepository messageRepo,
            ICredentialService routingService,
            IQueuePublisher publisher,
            ILogger<OrchestratorService> logger)
        {
            _messageRepo = messageRepo;
            _credentialService = routingService;
            _publisher = publisher;
            _logger = logger;
        }

        public async Task<OperationResult<SendMessageResponse>> ProcessSendRequestAsync(SendMessageRequest request, long projectId, DateTime apiReceivedAtUtc)
        {
            request.TraceId = Activity.Current?.TraceId.ToString();

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
                CreatedAt = apiReceivedAtUtc,
                TraceId = request.TraceId
            };

            await _messageRepo.AddMessageRequestAsync(messageRequest);

            _logger.LogInformation(
                "Request {RequestId} persisted with API receive timestamp {ApiReceivedAtUtc}. TraceId={TraceId}",
                request.RequestId,
                apiReceivedAtUtc,
                request.TraceId);

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
                        TraceId = request.TraceId,
                        Status = taskEntity.Status,
                        CreatedAt = taskEntity.CreatedAt
                    };

                    var queueName = QueueNames.GetChannelQueueName(recipient.Channel);
                    await _publisher.PublishAsync(queueName, taskDto);

                    _logger.LogInformation(
                        "Queued message task {TaskId} to {QueueName}. RequestId={RequestId}, Channel={Channel}, Recipient={Recipient}, CredentialId={CredentialId}, TraceId={TraceId}",
                        taskDto.Id,
                        queueName,
                        taskDto.RequestId,
                        taskDto.Channel,
                        taskDto.Recipient,
                        taskDto.CredentialId,
                        taskDto.TraceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to queue message for RequestId={RequestId}, Channel={Channel}, Recipient={Recipient}, TraceId={TraceId}",
                        request.RequestId,
                        recipient.Channel,
                        recipient.Recipient,
                        request.TraceId);

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
