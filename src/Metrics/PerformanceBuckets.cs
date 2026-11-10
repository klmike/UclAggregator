namespace UclAggregator.Metrics;

public sealed record SpeedBuckets<T>(IReadOnlyList<T> Fast, IReadOnlyList<T> Average, IReadOnlyList<T> Slow);

public sealed record MetricsResponse<T>(SpeedBuckets<T> Latest, SpeedBuckets<T> History);

public static class PerformanceBuckets
{
    public const double FastMaxMs = 100;
    public const double AverageMaxMs = 200;

    public static MetricsResponse<T> Group<T>(
        IReadOnlyDictionary<string, T> latestByName,
        IReadOnlyDictionary<string, IReadOnlyList<T>> allByName,
        Func<T, double> responseTime,
        Func<T, int> calls)
    {
        List<T> fastHistory = [], fastLatest = [];
        List<T> averageHistory = [], averageLatest = [];
        List<T> slowHistory = [], slowLatest = [];

        foreach (var name in allByName.Keys.Union(latestByName.Keys))
        {
            IReadOnlyList<T> windows = allByName.TryGetValue(name, out var stored) ? stored : [];
            latestByName.TryGetValue(name, out var latest);

            // Current window picks the bucket. With no live calls, use the weighted history average.
            double ms;
            if (latest is not null && calls(latest) > 0)
            {
                ms = responseTime(latest);
            }
            else
            {
                var weight = windows.Sum(calls);
                ms = weight == 0 ? 0 : windows.Sum(w => responseTime(w) * calls(w)) / weight;
            }

            var (historyBucket, latestBucket) = ms < FastMaxMs
                ? (fastHistory, fastLatest)
                : ms <= AverageMaxMs
                    ? (averageHistory, averageLatest)
                    : (slowHistory, slowLatest);

            historyBucket.AddRange(windows);
            if (latest is not null)
                latestBucket.Add(latest);
        }

        return new MetricsResponse<T>(
            new SpeedBuckets<T>(fastLatest, averageLatest, slowLatest),
            new SpeedBuckets<T>(fastHistory, averageHistory, slowHistory));
    }
}
