using API.Models;
using Abstractions.Models.Enums;
using ChildrenCharity.Mailing.Core.Infrastructure.Common;
using Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.Interfaces;

namespace API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class CredentialsController : ControllerBase
    {
        private readonly ICredentialRepository _credentialRepository;
        private readonly IAccountService _accountService;
        private readonly ILogger<CredentialsController> _logger;

        public CredentialsController(
            ICredentialRepository credentialRepository,
            IAccountService accountService,
            ILogger<CredentialsController> logger)
        {
            _credentialRepository = credentialRepository;
            _accountService = accountService;
            _logger = logger;
        }

        /// <summary>
        /// Создать новый credential
        /// </summary>
        /// <param name="request">Данные для создания credential</param>
        /// <returns>Созданный credential</returns>
        /// <response code="200">Credential успешно создан</response>
        /// <response code="400">Ошибка валидации</response>
        [HttpPost]
        [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OperationResult), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CredentialResponse>> CreateCredential([FromBody] CreateCredentialRequest request)
        {
            try
            {
                var projectId = await _accountService.GetProjectIdFromAuthorized();

                var credentialInfo = await _credentialRepository.AddCredentialAsync(
                    projectId,
                    request.Channel,
                    request.AdapterType,
                    request.Config);

                var credential = await _credentialRepository.GetByIdAsync(credentialInfo.CredentialId);

                if (credential == null)
                {
                    return BadRequest(OperationResult.Failure(Error.Internal("Failed to retrieve created credential")));
                }

                var response = new CredentialResponse
                {
                    Id = credential.Id,
                    ProjectId = credential.ProjectId,
                    Channel = credential.Channel.ToString(),
                    AdapterType = credential.AdapterType,
                    Config = credential.Config,
                    IsActive = credential.IsActive,
                    CreatedAt = credential.CreatedAt,
                    UpdatedAt = credential.UpdatedAt
                };

                _logger.LogInformation(
                    "Created credential {CredentialId} for ProjectId={ProjectId}, Channel={Channel}",
                    credential.Id, projectId, request.Channel);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating credential");
                return BadRequest(OperationResult.Failure(Error.Internal($"Failed to create credential: {ex.Message}")));
            }
        }

        /// <summary>
        /// Получить все credentials для текущего проекта
        /// </summary>
        /// <returns>Список credentials</returns>
        /// <response code="200">Список credentials получен успешно</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CredentialResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<CredentialResponse>>> GetCredentials()
        {
            try
            {
                var projectId = await _accountService.GetProjectIdFromAuthorized();

                var credentials = await _credentialRepository.FindAsync(c => c.ProjectId == projectId);

                var response = credentials.Select(c => new CredentialResponse
                {
                    Id = c.Id,
                    ProjectId = c.ProjectId,
                    Channel = c.Channel.ToString(),
                    AdapterType = c.AdapterType,
                    Config = c.Config,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving credentials");
                return BadRequest(OperationResult.Failure(Error.Internal($"Failed to retrieve credentials: {ex.Message}")));
            }
        }

        /// <summary>
        /// Получить credentials по каналу
        /// </summary>
        /// <param name="channel">Название канала</param>
        /// <returns>Список credentials для канала</returns>
        /// <response code="200">Список credentials получен успешно</response>
        [HttpGet("channel/{channel}")]
        [ProducesResponseType(typeof(IEnumerable<CredentialResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<CredentialResponse>>> GetCredentialsByChannel(ChannelType channel)
        {
            try
            {
                var projectId = await _accountService.GetProjectIdFromAuthorized();

                var credentialInfos = await _credentialRepository.GetCredentialsByProjectAndChannelAsync(projectId, channel);

                if (credentialInfos == null || !credentialInfos.Any())
                {
                    return Ok(Array.Empty<CredentialResponse>());
                }

                var credentials = await _credentialRepository.FindAsync(c => 
                    c.ProjectId == projectId && 
                    c.Channel == channel);

                var response = credentials.Select(c => new CredentialResponse
                {
                    Id = c.Id,
                    ProjectId = c.ProjectId,
                    Channel = c.Channel.ToString(),
                    AdapterType = c.AdapterType,
                    Config = c.Config,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving credentials for channel {Channel}", channel);
                return BadRequest(OperationResult.Failure(Error.Internal($"Failed to retrieve credentials: {ex.Message}")));
            }
        }

        /// <summary>
        /// Получить credential по ID
        /// </summary>
        /// <param name="id">ID credential</param>
        /// <returns>Credential</returns>
        /// <response code="200">Credential найден</response>
        /// <response code="404">Credential не найден</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CredentialResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CredentialResponse>> GetCredential(long id)
        {
            try
            {
                var projectId = await _accountService.GetProjectIdFromAuthorized();

                var credential = await _credentialRepository.GetByIdAsync(id);

                if (credential == null || credential.ProjectId != projectId)
                {
                    return NotFound();
                }

                var response = new CredentialResponse
                {
                    Id = credential.Id,
                    ProjectId = credential.ProjectId,
                    Channel = credential.Channel.ToString(),
                    AdapterType = credential.AdapterType,
                    Config = credential.Config,
                    IsActive = credential.IsActive,
                    CreatedAt = credential.CreatedAt,
                    UpdatedAt = credential.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving credential {CredentialId}", id);
                return BadRequest(OperationResult.Failure(Error.Internal($"Failed to retrieve credential: {ex.Message}")));
            }
        }

        /// <summary>
        /// Деактивировать credential
        /// </summary>
        /// <param name="id">ID credential</param>
        /// <returns>Результат операции</returns>
        /// <response code="200">Credential деактивирован</response>
        /// <response code="404">Credential не найден</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeactivateCredential(long id)
        {
            try
            {
                var projectId = await _accountService.GetProjectIdFromAuthorized();

                var credential = await _credentialRepository.GetByIdAsync(id);

                if (credential == null || credential.ProjectId != projectId)
                {
                    return NotFound();
                }

                credential.IsActive = false;
                credential.UpdatedAt = DateTime.UtcNow;

                _credentialRepository.Update(credential);
                await _credentialRepository.SaveChangesAsync();

                _logger.LogInformation(
                    "Deactivated credential {CredentialId} for ProjectId={ProjectId}",
                    id, projectId);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating credential {CredentialId}", id);
                return BadRequest(OperationResult.Failure(Error.Internal($"Failed to deactivate credential: {ex.Message}")));
            }
        }
    }
}
