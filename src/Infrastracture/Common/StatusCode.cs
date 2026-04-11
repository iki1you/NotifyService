namespace ChildrenCharity.Mailing.Core.Infrastructure.Common;

public enum StatusCode
{
    Ok = 200,
    Created = 201,
    Accepted = 202,
    NoContent = 204,
    ResetContent = 205,

    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409,
    Gone = 410,
    PreconditionFailed = 412,
    UnprocessableEntity = 422,
    TooManyRequests = 429,

    InternalServiceError = 500,
    NotImplemented = 501,
    BadGateway = 502,
    ServiceUnavailable = 503,
    GatewayTimeout = 504,
}