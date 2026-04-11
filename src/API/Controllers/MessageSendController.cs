using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orchestrator.Interfaces;
using Orchestrator.Models;
using System.Diagnostics;

namespace API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class MessageSendController : ControllerBase
    {
        private readonly IOrchestratorService _orchestratorService;
        private readonly IAccountService _accountService;
        private readonly ILogger<MessageSendController> _logger;

        public MessageSendController(
            IOrchestratorService orchestratorService,
            IAccountService accountService,
            ILogger<MessageSendController> logger)
        {
            _orchestratorService = orchestratorService;
            _accountService = accountService;
            _logger = logger;
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
            var apiReceivedAt = DateTime.UtcNow;
            var projectId = await _accountService.GetProjectIdFromAuthorized();
            var effectiveTraceId = Activity.Current?.TraceId.ToString();

            request.TraceId = effectiveTraceId;

            Activity.Current?.SetTag("request.id", request.RequestId);
            Activity.Current?.AddBaggage("request.id", request.RequestId.ToString());
            Activity.Current?.SetTag("request.trace_id", effectiveTraceId);

            _logger.LogInformation(
                "API accepted send request {RequestId} for project {ProjectId} at {ApiReceivedAtUtc}. TraceId={TraceId}",
                request.RequestId,
                projectId,
                apiReceivedAt,
                effectiveTraceId);

            var result = await _orchestratorService.ProcessSendRequestAsync(request, projectId, apiReceivedAt);

            return result.IsFail ? BadRequest(result) : Ok(result.Result);
        }
    }
}
