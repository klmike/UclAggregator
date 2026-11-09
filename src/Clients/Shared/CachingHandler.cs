using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UclAggregator.Metrics.Clients;
using UclAggregator.Options;

namespace UclAggregator.Clients;

public sealed class CachingHandler(IMemoryCache cache, IOptions<AggregatorOptions> options)
    : DelegatingHandler
{
    private sealed record Entry(HttpStatusCode Status, byte[] Body, MediaTypeHeaderValue? ContentType);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri is null)
        {
            return await base.SendAsync(request, ct);
        }

        var key = request.RequestUri.AbsoluteUri;
        var ttl = TimeSpan.FromHours(options.Value.CacheHours);
        var hit = true;
        var lazy = cache.GetOrCreate(key, e =>
        {
            hit = false;
            e.AbsoluteExpirationRelativeToNow = ttl;
            // Shared fetch must not cancel with the first caller; each waiter uses its own token.
            return new Lazy<Task<Entry>>(() => FetchAsync(request, key));
        })!;

        if (request.Options.TryGetValue(RequestMetrics.Key, out var metrics))
        {
            metrics.CacheUsed = hit;
        }

        var entry = await lazy.Value.WaitAsync(ct);

        var response = new HttpResponseMessage(entry.Status) { RequestMessage = request, Content = new ByteArrayContent(entry.Body) };
        response.Content.Headers.ContentType = entry.ContentType;
        return response;
    }

    private async Task<Entry> FetchAsync(HttpRequestMessage request, string key)
    {
        try
        {
            using var response = await base.SendAsync(request, CancellationToken.None);
            var body = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);
            if (!response.IsSuccessStatusCode)
            {
                cache.Remove(key);
            }
            return new Entry(response.StatusCode, body, response.Content.Headers.ContentType);
        }
        catch
        {
            cache.Remove(key);
            throw;
        }
    }
}
