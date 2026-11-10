using UclAggregator.Errors;

namespace UclAggregator.Metrics.Api;

public sealed record ApiStats(
    string CallName,
    DateTimeOffset Timestamp,
    double AverageResponseTime,
    int Calls,
    int Errors,
    double ErrorRate,
    IReadOnlyDictionary<ErrorCode, int> ErrorCodes)
{
    public static ApiStats From(string callName, DateTimeOffset timestamp, IReadOnlyList<ApiCallSample> samples)
    {
        var errors = samples.Count(s => s.ErrorCode != ErrorCode.Success);
        return new ApiStats(
            callName,
            timestamp,
            AverageResponseTime: samples.Average(s => s.ResponseTimeMs),
            Calls: samples.Count,
            Errors: errors,
            ErrorRate: (double)errors / samples.Count,
            ErrorCodes: samples
                .Where(s => s.ErrorCode != ErrorCode.Success)
                .GroupBy(s => s.ErrorCode)
                .ToDictionary(g => g.Key, g => g.Count()));
    }
}
