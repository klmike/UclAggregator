using System.Diagnostics;

namespace UclAggregator.Metrics.Clients;

public sealed class RequestMetrics
{
    public static readonly HttpRequestOptionsKey<RequestMetrics> Key = new("UclAggregator.RequestMetrics");

    public bool CacheUsed { get; set; }
}

public sealed class ClientMetricsHandler(string clientName, SampleBuffer<ClientCallSample> store) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var metrics = new RequestMetrics();
        request.Options.Set(RequestMetrics.Key, metrics);
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var timedOut = false;
        var clientError = false;

        try
        {
            var response = await base.SendAsync(request, ct);
            clientError = !response.IsSuccessStatusCode;
            return response;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            timedOut = ex is TimeoutException or OperationCanceledException;
            clientError = !timedOut;
            throw;
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                var apiCall = !metrics.CacheUsed;
                store.Record(new ClientCallSample(
                    clientName,
                    startedAt,
                    ResponseTimeMs: apiCall ? stopwatch.Elapsed.TotalMilliseconds : 0,
                    ApiCall: apiCall,
                    CacheUsed: metrics.CacheUsed,
                    TimedOut: timedOut,
                    ClientError: clientError));
            }
        }
    }
}
