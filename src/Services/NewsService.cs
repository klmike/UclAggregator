using Microsoft.Extensions.Options;
using UclAggregator.Clients;
using UclAggregator.Errors;
using UclAggregator.Models;
using UclAggregator.Options;

namespace UclAggregator.Services;

public sealed class NewsService(
    IEnumerable<INewsSource> sources,
    IOptions<AggregatorOptions> options,
    ILogger<NewsService> logger)
{
    // a failing provider never fails the request, it just ends up as a warning on the response
    public async Task<IReadOnlyList<NewsArticle>> SearchAsync(
        string terms, DateOnly? from, DateOnly? to, string? team, ApiResponse response, CancellationToken ct)
    {
        var query = new NewsQuery(terms, from, to, team);
        var applicable = sources.Where(s => s.AppliesTo(query)).ToList();
        // fallback sources start in parallel too, so falling back only uses data that is already on its way
        var primary = Start(applicable.Where(s => !s.Fallback));
        var fallback = Start(applicable.Where(s => s.Fallback));
        var found = await Collect(primary);
        // only use the fallback sources if the primaries came back thin
        if (found.Count <= options.Value.NewsFallbackMaxArticles)
            found.AddRange(await Collect(fallback));
        else
            // already running, so just observe the exception to keep it from going unobserved
            _ = Task.WhenAll(fallback.Select(c => c.Task)).ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        return found;

        List<(string Name, Task<IReadOnlyList<NewsArticle>> Task)> Start(IEnumerable<INewsSource> batch) =>
            batch.Select(s => (s.Name, Task: SearchSourceAsync(s, query, ct))).ToList();

        async Task<List<NewsArticle>> Collect(List<(string Name, Task<IReadOnlyList<NewsArticle>> Task)> calls)
        {
            // A caller abort propagates. A provider fault or HttpClient timeout is a warning.
            try { await Task.WhenAll(calls.Select(c => c.Task)); }
            catch (Exception) when (!ct.IsCancellationRequested) { }

            foreach (var failed in calls.Where(c => !c.Task.IsCompletedSuccessfully))
            {
                var error = failed.Task.Exception?.GetBaseException()
                    ?? new TimeoutException($"News provider '{failed.Name}' timed out.");
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

    // same title from several sources counts once (best score wins), then best score first, newest as tie-break
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
            // earlier results rank higher, and it never quite reaches 0
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

    // weight halves every NewsHalfLifeDays, articles without a date get a flat 0.5
    private double RecencyWeight(DateTimeOffset? published) =>
        published is null ? 0.5 : Math.Pow(0.5, Math.Max(0, (DateTimeOffset.UtcNow - published.Value).TotalDays) / options.Value.NewsHalfLifeDays);

    private static string Normalize(string? title) =>
        string.Join(' ', (title ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
