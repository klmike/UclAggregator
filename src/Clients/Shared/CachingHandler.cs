using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UclAggregator.Metrics.Clients;
using UclAggregator.Options;

namespace UclAggregator.Clients;

// Successful GETs are reused for CacheHours. Anything else is returned as-is so the next call can try again.
public sealed class CachingHandler(IMemoryCache cache, IOptions<AggregatorOptions> options) : DelegatingHandler
{
    private sealed record Entry(HttpStatusCode Status, byte[] Body, MediaTypeHeaderValue? ContentType);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri is null)
            return await base.SendAsync(request, ct);

        var key = request.RequestUri.AbsoluteUri;
        if (cache.TryGetValue(key, out Entry? cached) && cached is not null)
        {
            if (request.Options.TryGetValue(RequestMetrics.Key, out var metrics))
                metrics.CacheUsed = true;
            return Copy(request, cached);
        }

        var response = await base.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            return response;

        var body = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType;
        cache.Set(key, new Entry(response.StatusCode, body, contentType), TimeSpan.FromHours(options.Value.CacheHours));
        response.Content = new ByteArrayContent(body);
        response.Content.Headers.ContentType = contentType;
        return response;
    }

    private static HttpResponseMessage Copy(HttpRequestMessage request, Entry entry)
    {
        var response = new HttpResponseMessage(entry.Status)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(entry.Body),
        };
        response.Content.Headers.ContentType = entry.ContentType;
        return response;
    }
}
