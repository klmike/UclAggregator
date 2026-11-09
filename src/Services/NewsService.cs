using Microsoft.Extensions.Options;
using UclAggregator.Clients;
using UclAggregator.Errors;
using UclAggregator.Models;
using UclAggregator.Options;

namespace UclAggregator.Services;

/// <summary>Aggregates all registered <see cref="INewsSource"/>s. Score = provider relevance weighted by recency.</summary>
public sealed class NewsService(
    IEnumerable<INewsSource> sources,
    IOptions<AggregatorOptions> options,
    ILogger<NewsService> logger)
{
    public async Task<IReadOnlyList<NewsArticle>> SearchAsync(
        string terms, DateOnly? from, DateOnly? to, string? team, ApiResponse response, CancellationToken ct)
    {
        var query = new NewsQuery(terms, from, to, team);
        var applicable = sources.Where(s => s.AppliesTo(query)).ToList();
        var found = await Run(applicable.Where(s => !s.Fallback).ToList());
        if (found.Count <= options.Value.NewsFallbackMaxArticles)
            found.AddRange(await Run(applicable.Where(s => s.Fallback).ToList()));
        return found;

        async Task<List<NewsArticle>> Run(List<INewsSource> batch)
        {
            var calls = batch.Select(s => (s.Name, Task: SearchSourceAsync(s, query, ct))).ToList();
            try
            {
                await Task.WhenAll(calls.Select(c => c.Task));
            }
            catch
            {
                // Individual failures are inspected below; one provider failing must not drop the others.
            }

            foreach (var failed in calls.Where(c => c.Task.IsFaulted))
            {
                var error = failed.Task.Exception!.GetBaseException();
                var apiError = ApiError.From(error, $"News provider '{failed.Name}'");
                logger.LogWarning(error, "News provider {Provider} failed with {Code}", failed.Name, apiError.Code);
                response.AddWarning(apiError);
            }

            var ok = calls.Where(c => c.Task.IsCompletedSuccessfully).ToList();
            logger.LogInformation("News search '{Terms}' (team={Team}): {Ok}/{Total} providers succeeded, {Articles} articles",
                terms, team, ok.Count, calls.Count, ok.Sum(c => c.Task.Result.Count));
            if (calls.Count > 0 && ok.Count == 0)
                logger.LogError("All {Total} news providers failed for '{Terms}'", calls.Count, terms);

            return ok.SelectMany(c => c.Task.Result).ToList();
        }
    }

    public static IReadOnlyList<NewsArticle> Combine(IReadOnlyList<NewsArticle> articles, int limit) =>
        Combine([articles], limit);

    public static IReadOnlyList<NewsArticle> Combine(IEnumerable<IReadOnlyList<NewsArticle>> lists, int limit) =>
        lists.SelectMany(l => l)
            .GroupBy(a => Normalize(a.Title))
            .Select(g => g.OrderByDescending(a => a.Score).First())
            .OrderByDescending(a => a.Score)
            .ThenByDescending(a => a.PublishedAt)
            .Take(limit)
            .ToList();

    private async Task<IReadOnlyList<NewsArticle>> SearchSourceAsync(INewsSource source, NewsQuery query, CancellationToken ct)
    {
        var list = await source.SearchAsync(query, ct);
        return list.Select((a, idx) =>
        {
            // Relevance is the provider's own ordering, scaled to (0, 1].
            var relevance = 1.0 - (double)idx / (list.Count + 1);
            return new NewsArticle
            {
                Id = a.Id,
                Source = source.Name,
                Publisher = a.Publisher,
                Title = a.Title,
                Url = Uri.TryCreate(a.Url, UriKind.Absolute, out var url) ? url : null,
                PublishedAt = a.PublishedAt,
                Relevance = Math.Round(relevance, 4),
                Score = Math.Round(relevance * RecencyWeight(a.PublishedAt), 4),
            };
        }).ToList();
    }

    // Exponential decay: an article loses half its weight every NewsHalfLifeDays; undated articles count as half.
    private double RecencyWeight(DateTimeOffset? published) =>
        published is null ? 0.5 : Math.Pow(0.5, Math.Max(0, (DateTimeOffset.UtcNow - published.Value).TotalDays) / options.Value.NewsHalfLifeDays);

    private static string Normalize(string? title) =>
        string.Join(' ', (title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
