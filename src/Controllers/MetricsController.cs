using Microsoft.AspNetCore.Mvc;
using UclAggregator.Metrics;
using UclAggregator.Metrics.Api;
using UclAggregator.Metrics.Clients;

namespace UclAggregator.Controllers;

[ApiController]
[Route("metrics")]
public class MetricsController(StatsHistory<ClientStats> clientStats, StatsHistory<ApiStats> apiStats) : ControllerBase
{
    [HttpGet("clients")]
    public ClientMetricsResponse Clients() => ClientMetricsResponse.From(clientStats);

    [HttpGet("api")]
    public ApiMetricsResponse Api() => ApiMetricsResponse.From(apiStats);
}
