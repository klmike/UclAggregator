namespace UclAggregator.Metrics.Clients;

/// <summary>Client metrics grouped by historical average response time. Latest is returned but not used for the bucket.</summary>
public sealed record ClientMetricsResponse(
    MetricWindows<ClientStats> Fast,
    MetricWindows<ClientStats> Average,
    MetricWindows<ClientStats> Slow)
{
    public static ClientMetricsResponse From(StatsHistory<ClientStats> store)
    {
        var grouped = PerformanceBuckets.Group(
            store.GetLatest(), store.GetAll(), s => s.AverageResponseTime, s => s.ApiCalls);
        return new(grouped.Fast, grouped.Average, grouped.Slow);
    }
}
