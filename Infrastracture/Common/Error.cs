namespace ChildrenCharity.Mailing.Core.Infrastructure.Common;

public class Error
{
    public Error(StatusCode statusCode, string message)
    {
        StatusCode = statusCode;
        Message = message;
    }

    public StatusCode StatusCode { get; }
    public string Message { get; }

    public static Error Internal(string message)
    {
        return new Error(StatusCode.InternalServiceError, message);
    }

    public static Error BadRequest(string message)
    {
        return new Error(StatusCode.BadRequest, message);
    }

    public static Error NotFound(string message)
    {
        return new Error(StatusCode.NotFound, message);
    }

    public static Error Conflict(string message)
    {
        return new Error(StatusCode.Conflict, message);
    }

    public static Error Unauthorized(string message)
    {
        return new Error(StatusCode.Unauthorized, message);
    }

    public static Error Forbidden(string message)
    {
        return new Error(StatusCode.Forbidden, message);
    }

    public static Error PreconditionFailed(string message)
    {
        return new Error(StatusCode.PreconditionFailed, message);
    }

    public static Error UnprocessableEntity(string message)
    {
        return new Error(StatusCode.UnprocessableEntity, message);
    }

    public static Error TooManyRequests(string message)
    {
        return new Error(StatusCode.TooManyRequests, message);
    }

    public static Error BadGateway(string message)
    {
        return new Error(StatusCode.BadGateway, message);
    }

    public static Error ServiceUnavailable(string message)
    {
        return new Error(StatusCode.ServiceUnavailable, message);
    }

    public static Error GatewayTimeout(string message)
    {
        return new Error(StatusCode.GatewayTimeout, message);
    }

    public override string ToString()
    {
        return $"Error: StatusCode = {(int) StatusCode}, Message = '{Message}'";
    }
}