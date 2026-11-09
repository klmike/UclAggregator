using Microsoft.AspNetCore.Mvc;
using UclAggregator.Errors;
using UclAggregator.Metrics.Api;
using UclAggregator.Models;

namespace UclAggregator.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> Respond<T>(T response) where T : ApiResponse
    {
        if (response.Error is not { } error)
        {
            return response;
        }

        HttpContext.Items[ApiMetricsMiddleware.ErrorCodeKey] = error.Code;
        HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType())
            .LogWarning("{Path} -> {Code}: {Message}", Request.Path, error.Code, error.Message);
        return StatusCode(error.Code.ToStatusCode(), response.ToErrorResponse());
    }
}
