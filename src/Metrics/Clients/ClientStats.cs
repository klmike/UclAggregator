namespace UclAggregator.Metrics.Clients;

/// <summary>Aggregated client metrics for one client over one job window.</summary>
/// <param name="AverageResponseTime">Mean response time (ms) of the API calls only; 0 when there were none.</param>
/// <param name="ApiCalls">Calls that went out to the API.</param>
/// <param name="CacheCalls">Calls served from cache.</param>
/// <param name="TimedOut">Calls that timed out.</param>
/// <param name="Errors">API calls that failed: a client error or a timeout.</param>
/// <param name="ErrorRate">Errors per API call (0..1); 0 when there were no API calls.</param>
public sealed record ClientStats(
    string ClientName,
    DateTimeOffset Timestamp,
    double AverageResponseTime,
    int ApiCalls,
    int CacheCalls,
    int TimedOut,
    int Errors,
    double ErrorRate);
