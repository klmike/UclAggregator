using UclAggregator.Errors;

namespace UclAggregator.Metrics.Api;

/// <summary>Aggregated metrics for one API call over one job window.</summary>
/// <param name="AverageResponseTime">Mean response time (ms) of all calls, failed ones included.</param>
/// <param name="Calls">Number of calls.</param>
/// <param name="Errors">Calls that did not end in <see cref="ErrorCode.Success"/>.</param>
/// <param name="ErrorRate">Errors per call (0..1).</param>
/// <param name="ErrorCodes">Number of calls per error code, for diagnosis.</param>
public sealed record ApiStats(
    string CallName,
    DateTimeOffset Timestamp,
    double AverageResponseTime,
    int Calls,
    int Errors,
    double ErrorRate,
    IReadOnlyDictionary<ErrorCode, int> ErrorCodes);
