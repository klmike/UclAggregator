using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using UclAggregator.Errors;

namespace UclAggregator.Metrics.Api;

public sealed class ApiMetricsMiddleware(RequestDelegate next, SampleBuffer<ApiCallSample> store)
{
    public const string ErrorCodeKey = "UclAggregator.ErrorCode";

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        ErrorCode? failure = null;

        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            failure = ApiError.From(ex, "The request").Code; // the exception handler runs after us and cannot tell us
            throw;
        }
        finally
        {
            var endpoint = context.GetEndpoint();
            var matched = endpoint is not null;
            var isController = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is not null;

            // Skip aborted requests and non-controller endpoints (OpenAPI, Scalar); unmatched routes are always recorded.
            if (!context.RequestAborted.IsCancellationRequested && (!matched || isController))
            {
                store.Record(new ApiCallSample(
                    CallName: (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched",
                    startedAt,
                    stopwatch.Elapsed.TotalMilliseconds,
                    failure ?? GetErrorCode(context),
                    matched));
            }
        }
    }

    private static ErrorCode GetErrorCode(HttpContext context)
    {
        if (context.Items.TryGetValue(ErrorCodeKey, out var code) && code is ErrorCode errorCode)
        {
            return errorCode;
        }
        return context.Response.StatusCode < 400 ? ErrorCode.Success : ErrorCode.Unexpected;
    }
}
