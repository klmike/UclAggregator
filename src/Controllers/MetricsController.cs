using Microsoft.AspNetCore.Mvc;
using UclAggregator.Metrics;
using UclAggregator.Metrics.Api;
using UclAggregator.Metrics.Clients;

namespace UclAggregator.Controllers;

[ApiController]
[Route("metrics")]
public class MetricsController(
    StatsHistory<ClientStats> clientStats,
    SampleBuffer<ClientCallSample> clientSamples,
    StatsHistory<ApiStats> apiStats,
    SampleBuffer<ApiCallSample> apiSamples) : ControllerBase
{
    [HttpGet("clients")]
    public MetricsResponse<ClientStats> Clients() => ClientMetricsResponse.From(clientStats, clientSamples);

    [HttpGet("api")]
    public MetricsResponse<ApiStats> Api() => ApiMetricsResponse.From(apiStats, apiSamples);
}
