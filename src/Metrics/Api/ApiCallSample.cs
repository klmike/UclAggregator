using UclAggregator.Errors;

namespace UclAggregator.Metrics.Api;

public sealed record ApiCallSample(
    string CallName,
    DateTimeOffset Timestamp,
    double ResponseTimeMs,
    ErrorCode ErrorCode) : ISample;
