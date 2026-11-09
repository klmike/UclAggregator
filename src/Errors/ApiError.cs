using System.Net;
using System.Text.Json;

namespace UclAggregator.Errors;

public sealed record ApiError(ErrorCode Code, string Message)
{
    /// <summary>
    /// Translates an exception into an error. <paramref name="what"/> names the failed operation; the exception text is
    /// deliberately not echoed to callers (it is logged instead).
    /// </summary>
    public static ApiError From(Exception ex, string what) => ex switch
    {
        AppException app => app.Error,
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            new(ErrorCode.ProviderRateLimited, $"{what} is rate limited by the upstream provider."),
        TimeoutException or TaskCanceledException =>
            new(ErrorCode.ProviderTimeout, $"{what} timed out."),
        JsonException => new(ErrorCode.ProviderBadResponse, $"{what} returned an unreadable response."),
        HttpRequestException =>
            new(ErrorCode.ProviderUnavailable, $"{what} is unavailable."),
        _ => new(ErrorCode.Unexpected, $"{what} failed unexpectedly."),
    };
}

/// <summary>Thrown when a layer detects a failure it owns; the exception handler turns it into the matching HTTP response.</summary>
public sealed class AppException(ErrorCode code, string message, Exception? inner = null) : Exception(message, inner)
{
    public ApiError Error { get; } = new(code, message);
}
