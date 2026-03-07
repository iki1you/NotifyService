using Abstractions.Models;
using Data.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Queue.AbstractWorkers;
using Queue.Constants;
using Queue.Services;

namespace Orchestrator.Workers
{
    /// <summary>
    /// Воркер для обработки статусов отправки сообщений.
    /// Принимает сообщения из очереди статусов и обновляет информацию в БД.
    /// </summary>
    public class MessageStatusWorker : SingleConsumerWorker<MessageTaskStatusDTO>
    {
        public MessageStatusWorker(
            ILogger<MessageStatusWorker> logger,
            IServiceScopeFactory serviceScopeFactory,
            IRabbitMqConnectionFactory connectionFactory)
            : base(logger, serviceScopeFactory, connectionFactory, QueueNames.MessageStatusUpdates, "MessageStatusWorker")
        {
        }

        protected override async Task ProcessMessageAsync(MessageTaskStatusDTO statusUpdate)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var messageRepository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();

            // Получаем задачу из БД
            var messageTask = await messageRepository.GetMessageTaskByIdAsync(statusUpdate.MessageTaskId);

            if (messageTask == null)
            {
                throw new InvalidOperationException($"MessageTask {statusUpdate.MessageTaskId} not found in database");
            }

            // Обновляем статус
            messageTask.Status = statusUpdate.Status;

            await messageRepository.UpdateMessageTaskAsync(messageTask);
        }
    }
}
