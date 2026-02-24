namespace Orchestrator.Interfaces
{
    public interface IAccountService
    {
        Task<long> GetProjectIdFromAuthorized();
    }
}
