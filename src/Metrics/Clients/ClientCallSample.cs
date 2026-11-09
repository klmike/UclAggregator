namespace UclAggregator.Metrics.Clients;

/// <summary>One outgoing client call as seen by the caller.</summary>
/// <param name="ClientName">Typed client name, e.g. <c>EspnClient</c>.</param>
/// <param name="Timestamp">When the call started (UTC).</param>
/// <param name="ResponseTimeMs">Elapsed time of the call; 0 when the data came from cache (<paramref name="ApiCall"/> is false).</param>
/// <param name="ApiCall">True when the upstream API was actually called.</param>
/// <param name="CacheUsed">True when the data was found in the cache (including joining an in-flight fetch).</param>
/// <param name="TimedOut">True when the call timed out.</param>
/// <param name="ClientError">True when the call failed for a reason other than a timeout (exception or non-success status).</param>
public sealed record ClientCallSample(
    string ClientName,
    DateTimeOffset Timestamp,
    double ResponseTimeMs,
    bool ApiCall,
    bool CacheUsed,
    bool TimedOut,
    bool ClientError);
