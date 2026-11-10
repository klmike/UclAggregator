namespace UclAggregator.Metrics.Clients;

public sealed record ClientCallSample(
    string ClientName,
    DateTimeOffset Timestamp,
    double ResponseTimeMs,
    bool ApiCall,
    bool CacheUsed,
    bool TimedOut,
    bool ClientError) : ISample;
