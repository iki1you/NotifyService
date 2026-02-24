namespace ChildrenCharity.Mailing.Core.Infrastructure.Common;

public class OperationResult
{
    protected OperationResult(Error? error = null)
    {
        Error = error;
    }

    public Error? Error { get; }

    public bool IsSuccess => !IsFail;

    public bool IsFail => Error is not null;

    public static OperationResult Success()
    {
        return new OperationResult();
    }

    public static OperationResult Failure(Error error)
    {
        return new OperationResult(error);
    }

    public static implicit operator OperationResult(Error error)
    {
        return Failure(error);
    }
}