using Abstractions.Models;
using Abstractions.Models.Enums;
using Orchestrator.Models;
using Swashbuckle.AspNetCore.Filters;

namespace API.Swagger
{
    public class SendMessageRequestExample : IExamplesProvider<SendMessageRequest>
    {
        public SendMessageRequest GetExamples()
        {
            return new SendMessageRequest
            {
                RequestId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
                RecipientItems = new List<RecipientItem>
                {
                    new RecipientItem
                    {
                        Channel = ChannelType.Email,
                        Recipient = "user@example.com"
                    },
                    new RecipientItem
                    {
                        Channel = ChannelType.WhatsApp,
                        Recipient = "+79991234567"
                    }
                },
                Message = new Message
                {
                    Title = "Важное уведомление",
                    Content = "Здравствуйте! Это тестовое сообщение из NotifyService."
                },
                Delay = null
            };
        }
    }

    public class SendMessageResponseExample : IExamplesProvider<SendMessageResponse>
    {
        public SendMessageResponse GetExamples()
        {
            return new SendMessageResponse
            {
                RequestId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
                StatusUrl = "/api/status/550e8400-e29b-41d4-a716-446655440000",
                ChannelErrors = new List<ChannelError>()
            };
        }
    }
}
