namespace UclAggregator.Metrics.Clients;

public static class ClientMetricsResponse
{
    public static MetricsResponse<ClientStats> From(StatsHistory<ClientStats> store, SampleBuffer<ClientCallSample> samples)
    {
        var now = DateTimeOffset.UtcNow;
        var latest = samples.Recent()
            .GroupBy(s => s.ClientName)
            .ToDictionary(g => g.Key, g => ClientStats.From(g.Key, now, g.ToList()));
        return PerformanceBuckets.Group(latest, store.GetAll(), s => s.AverageResponseTime, s => s.ApiCalls);
    }
}
