using System.Collections.Concurrent;

namespace UclAggregator.Metrics;

public sealed class StatsHistory<T>
{
    // 7 days of 5 minute snapshots
    private const int MaxEntries = 2016;

    private readonly ConcurrentDictionary<string, List<T>> history = new();

    public void Add(string key, T stats)
    {
        // the dictionary is thread safe but the lists are not, so every list access takes its own lock
        var list = history.GetOrAdd(key, _ => []);
        lock (list)
        {
            list.Add(stats);
            if (list.Count > MaxEntries)
                list.RemoveAt(0);
        }
    }

    public IReadOnlyList<T> Get(string key)
    {
        if (!history.TryGetValue(key, out var list))
            return [];
        lock (list)
        {
            return [.. list];
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<T>> GetAll() =>
        history.Keys.ToDictionary(name => name, Get);
}
