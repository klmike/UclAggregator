namespace UclAggregator.Metrics;

/// <summary>History and latest windows for the requests that landed in one performance bucket.</summary>
public sealed record MetricWindows<T>(IReadOnlyList<T> History, IReadOnlyList<T> Latest);

/// <summary>Groups named series into fast (&lt;100 ms), average (100-200 ms) and slow (&gt;200 ms) using history only.</summary>
public static class PerformanceBuckets
{
    public const double FastMaxMs = 100;
    public const double AverageMaxMs = 200;

    public static (MetricWindows<T> Fast, MetricWindows<T> Average, MetricWindows<T> Slow) Group<T>(
        IReadOnlyDictionary<string, T> latestByName,
        IReadOnlyDictionary<string, IReadOnlyList<T>> allByName,
        Func<T, double> responseTime,
        Func<T, int> calls)
    {
        List<T> fastHistory = [], fastLatest = [];
        List<T> averageHistory = [], averageLatest = [];
        List<T> slowHistory = [], slowLatest = [];

        foreach (var (name, windows) in allByName)
        {
            latestByName.TryGetValue(name, out var latest);
            List<T> history = [];
            foreach (var window in windows)
            {
                if (latest is not null && ReferenceEquals(window, latest))
                {
                    continue;
                }
                history.Add(window);
            }

            var weight = history.Sum(calls);
            if (weight == 0)
            {
                continue;
            }

            var ms = history.Sum(w => responseTime(w) * calls(w)) / weight;
            var (bucketHistory, bucketLatest) = Bucket(ms);
            bucketHistory.AddRange(history);
            if (latest is not null)
            {
                bucketLatest.Add(latest);
            }
        }

        return (
            new MetricWindows<T>(fastHistory, fastLatest),
            new MetricWindows<T>(averageHistory, averageLatest),
            new MetricWindows<T>(slowHistory, slowLatest));

        (List<T> History, List<T> Latest) Bucket(double ms)
        {
            if (ms < FastMaxMs)
            {
                return (fastHistory, fastLatest);
            }
            if (ms <= AverageMaxMs)
            {
                return (averageHistory, averageLatest);
            }
            return (slowHistory, slowLatest);
        }
    }
}
