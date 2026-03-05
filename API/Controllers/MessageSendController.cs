using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.Interfaces;
using Orchestrator.Models;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessageSendController : ControllerBase
    {
        private readonly IOrchestratorService _orchestratorService;
        private readonly IAccountService _accountService;

        public MessageSendController(
            IOrchestratorService orchestratorService,
            IAccountService accountService) 
        {
            _orchestratorService = orchestratorService;
            _accountService = accountService;
        }

        /// <summary>
        /// Отправить сообщение через указанные каналы
        /// </summary>
        /// <param name="request">Запрос на отправку</param>
        /// <returns>Результат отправки с URL для проверки статуса</returns>
        /// <response code="200">Сообщение успешно добавлено в очередь</response>
        /// <response code="400">Ошибка валидации или дубликат запроса</response>
        [HttpPost]
        [ProducesResponseType(typeof(SendMessageResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult<SendMessageResponse>>> SendMessage([FromBody] SendMessageRequest request)
        {
            var projectId = await _accountService.GetProjectIdFromAuthorized();

            var result = await _orchestratorService.ProcessSendRequestAsync(request, projectId);

            return result.IsFail ? BadRequest(result) : Ok(result.Result);
        }
    }
}
