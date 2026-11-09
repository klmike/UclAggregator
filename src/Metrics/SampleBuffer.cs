using System.Collections.Concurrent;

namespace UclAggregator.Metrics;

public sealed class SampleBuffer<T>
{
    private readonly ConcurrentQueue<T> samples = new();

    public void Record(T sample) => samples.Enqueue(sample);

    public IReadOnlyList<T> Drain()
    {
        var drained = new List<T>();
        while (samples.TryDequeue(out var sample))
            drained.Add(sample);
        return drained;
    }
}
