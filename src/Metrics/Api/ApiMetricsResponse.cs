namespace UclAggregator.Metrics.Api;

public static class ApiMetricsResponse
{
    public static MetricsResponse<ApiStats> From(StatsHistory<ApiStats> store, SampleBuffer<ApiCallSample> samples)
    {
        var now = DateTimeOffset.UtcNow;
        var latest = samples.Recent()
            .GroupBy(s => s.CallName)
            .ToDictionary(g => g.Key, g => ApiStats.From(g.Key, now, g.ToList()));
        return PerformanceBuckets.Group(latest, store.GetAll(), s => s.AverageResponseTime, s => s.Calls);
    }
}
