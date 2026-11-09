using UclAggregator.Clients;
using UclAggregator.Models;

namespace UclAggregator.Tests.Support;

internal sealed class FakeNewsSource : INewsSource
{
    public required string Name { get; init; }
    public bool Fallback { get; init; }
    public Func<NewsQuery, bool>? Applies { get; init; }
    public IReadOnlyList<SourceArticle> Articles { get; init; } = [];
    public Exception? Fail { get; init; }
    public List<NewsQuery> Queries { get; } = [];

    public bool AppliesTo(NewsQuery query) => Applies?.Invoke(query) ?? true;

    public Task<IReadOnlyList<SourceArticle>> SearchAsync(NewsQuery query, CancellationToken ct)
    {
        Queries.Add(query);
        if (Fail is not null)
            throw Fail;
        return Task.FromResult(Articles);
    }
}
