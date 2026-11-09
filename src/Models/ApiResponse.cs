using System.Collections.Concurrent;
using UclAggregator.Errors;

namespace UclAggregator.Models;

public class ApiResponse
{
    private readonly ConcurrentQueue<ApiError> _warnings = new();

    public ApiError? Error { get; private set; }

    public IReadOnlyList<ApiError> Warnings => [.. _warnings];

    public void AddError(ErrorCode code, string message) => Error = new ApiError(code, message);

    public void AddWarning(ErrorCode code, string message) => _warnings.Enqueue(new ApiError(code, message));

    public void AddWarning(ApiError warning) => _warnings.Enqueue(warning);

    public static ApiResponse Failure(ErrorCode code, string message)
    {
        var response = new ApiResponse();
        response.AddError(code, message);
        return response;
    }

    public ApiResponse ToErrorResponse()
    {
        var response = new ApiResponse { Error = Error };
        foreach (var warning in _warnings)
        {
            response.AddWarning(warning);
        }
        return response;
    }
}
