using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.Interfaces;
using Orchestrator.Models;

namespace API.Controllers
{
    [ApiController]
    [Route("[controller]")]
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

        [HttpGet(Name = "SendMessage")]
        [ProducesDefaultResponseType(typeof(OperationResult))]
        [ProducesResponseType<OperationResult>(StatusCodes.Status200OK)]
        [ProducesResponseType<OperationResult>(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<OperationResult>> SendMessage([FromBody] SendMessageRequest request)
        {
            var projectId = await _accountService.GetProjectIdFromAuthorized();

            var result = await _orchestratorService.ProcessSendRequestAsync(request, projectId);

            return result.IsFail? BadRequest(): Ok();
        }
    }
}
