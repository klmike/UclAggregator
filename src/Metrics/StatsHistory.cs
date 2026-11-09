using System.Collections.Concurrent;

namespace UclAggregator.Metrics;

public sealed class StatsHistory<T>
{
    private const int MaxEntries = 2016;

    private readonly ConcurrentDictionary<string, List<T>> history = new();
    private readonly ConcurrentDictionary<string, T> latest = new();

    public void Add(string key, T stats)
    {
        latest[key] = stats;
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

    public IReadOnlyDictionary<string, T> GetLatest() => latest.ToDictionary();

    public IReadOnlyDictionary<string, IReadOnlyList<T>> GetAll() =>
        history.Keys.ToDictionary(name => name, Get);
}
