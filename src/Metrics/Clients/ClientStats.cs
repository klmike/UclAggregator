namespace UclAggregator.Metrics.Clients;

public sealed record ClientStats(
    string ClientName,
    DateTimeOffset Timestamp,
    double AverageResponseTime,
    int ApiCalls,
    int CacheCalls,
    int TimedOut,
    int Errors,
    double ErrorRate)
{
    public static ClientStats From(string clientName, DateTimeOffset timestamp, IReadOnlyList<ClientCallSample> samples)
    {
        var apiCalls = samples.Where(s => s.ApiCall).ToList();
        var errors = apiCalls.Count(s => s.ClientError || s.TimedOut);
        return new ClientStats(
            clientName,
            timestamp,
            AverageResponseTime: apiCalls.Count == 0 ? 0 : apiCalls.Average(s => s.ResponseTimeMs),
            ApiCalls: apiCalls.Count,
            CacheCalls: samples.Count(s => s.CacheUsed),
            TimedOut: samples.Count(s => s.TimedOut),
            Errors: errors,
            ErrorRate: apiCalls.Count == 0 ? 0 : (double)errors / apiCalls.Count);
    }
}
