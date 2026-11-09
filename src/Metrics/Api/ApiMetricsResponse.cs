namespace UclAggregator.Metrics.Api;

/// <summary>API metrics grouped by historical average response time. Latest is returned but not used for the bucket.</summary>
public sealed record ApiMetricsResponse(
    MetricWindows<ApiStats> Fast,
    MetricWindows<ApiStats> Average,
    MetricWindows<ApiStats> Slow)
{
    public static ApiMetricsResponse From(StatsHistory<ApiStats> store)
    {
        var grouped = PerformanceBuckets.Group(
            store.GetLatest(), store.GetAll(), s => s.AverageResponseTime, s => s.Calls);
        return new(grouped.Fast, grouped.Average, grouped.Slow);
    }
}
