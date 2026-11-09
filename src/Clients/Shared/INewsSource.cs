using UclAggregator.Models;

namespace UclAggregator.Clients;

public sealed record NewsQuery(string Terms, DateOnly? From, DateOnly? To, string? Team);

public interface INewsSource
{
    string Name { get; }

    bool AppliesTo(NewsQuery query);

    bool Fallback => false;

    Task<IReadOnlyList<SourceArticle>> SearchAsync(NewsQuery query, CancellationToken ct);
}
