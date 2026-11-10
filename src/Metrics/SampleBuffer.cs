using System.Collections.Concurrent;

namespace UclAggregator.Metrics;

// requests record samples concurrently. The background job drains the pending queue into history,
// while the recent queue keeps the last window of samples so the latest metrics can be calculated live
public sealed class SampleBuffer<T> where T : ISample
{
    public static readonly TimeSpan LatestWindow = TimeSpan.FromMinutes(5);

    private readonly ConcurrentQueue<T> pending = new();
    private readonly ConcurrentQueue<T> recent = new();
    private readonly object trimLock = new();

    public void Record(T sample)
    {
        pending.Enqueue(sample);
        recent.Enqueue(sample);
    }

    public IReadOnlyList<T> Drain()
    {
        var drained = new List<T>();
        while (pending.TryDequeue(out var sample))
            drained.Add(sample);
        return drained;
    }

    public IReadOnlyList<T> Recent()
    {
        var cutoff = DateTimeOffset.UtcNow - LatestWindow;
        lock (trimLock)
        {
            while (recent.TryPeek(out var oldest) && oldest.Timestamp < cutoff)
                recent.TryDequeue(out _);
        }
        return [.. recent.Where(s => s.Timestamp >= cutoff)];
    }
}
