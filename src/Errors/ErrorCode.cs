using System.Text.Json.Serialization;

namespace UclAggregator.Errors;
public enum ErrorCode
{
    Success = 0,
    InvalidRequest = 1000,
    Unauthorized = 1002,
    InvalidCredentials = 1003,
    TeamNotFound = 2000,
    MatchNotFound = 2001,
    PlayerNotFound = 2002,
    DataNotAvailable = 2003,
    ProviderUnavailable = 3000,
    ProviderTimeout = 3001,
    ProviderRateLimited = 3002,
    ProviderBadResponse = 3003,
    Unexpected = 9000,
}

public static class ErrorCodeExtensions
{
    //Maps Application Error Codes to HTTP Statues
    public static int ToStatusCode(this ErrorCode code) => code switch
    {
        ErrorCode.Success => StatusCodes.Status200OK,
        ErrorCode.InvalidRequest => StatusCodes.Status400BadRequest,
        ErrorCode.Unauthorized or ErrorCode.InvalidCredentials => StatusCodes.Status401Unauthorized,
        ErrorCode.TeamNotFound or ErrorCode.MatchNotFound or ErrorCode.PlayerNotFound => StatusCodes.Status404NotFound,
        ErrorCode.ProviderRateLimited => StatusCodes.Status503ServiceUnavailable,
        ErrorCode.ProviderTimeout => StatusCodes.Status504GatewayTimeout,
        ErrorCode.ProviderUnavailable or ErrorCode.ProviderBadResponse => StatusCodes.Status502BadGateway,
        _ => StatusCodes.Status500InternalServerError,
    };
}
