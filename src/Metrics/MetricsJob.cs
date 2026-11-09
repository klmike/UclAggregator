using UclAggregator.Errors;
using UclAggregator.Metrics.Api;
using UclAggregator.Metrics.Clients;

namespace UclAggregator.Metrics;

public sealed class MetricsJob(
    SampleBuffer<ClientCallSample> clientSamples,
    StatsHistory<ClientStats> clientHistory,
    SampleBuffer<ApiCallSample> apiSamples,
    StatsHistory<ApiStats> apiHistory,
    ILogger<MetricsJob> logger) : BackgroundService
{
    private const double Threshold = 1.5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RunClients();
                RunApi();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Metrics aggregation failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private void RunClients()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var group in clientSamples.Drain().GroupBy(s => s.ClientName))
        {
            var stats = ClientStatsFor(group.Key, now, group.ToList());
            CheckClientAnomalies(stats, clientHistory.Get(group.Key));
            clientHistory.Add(stats.ClientName, stats);
        }
    }

    private void RunApi()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var group in apiSamples.Drain().GroupBy(s => s.CallName))
        {
            var stats = ApiStatsFor(group.Key, now, group.ToList());
            CheckApiAnomalies(stats, apiHistory.Get(group.Key));
            apiHistory.Add(stats.CallName, stats);
        }
    }

    private static ClientStats ClientStatsFor(string clientName, DateTimeOffset timestamp, IReadOnlyList<ClientCallSample> samples)
    {
        var apiCalls = samples.Where(s => s.ApiCall).ToList();
        var errors = apiCalls.Count(s => s.ClientError || s.TimedOut);
        return new ClientStats(
            clientName,
            timestamp,
            AverageResponseTime: apiCalls.Count == 0 ? 0 : apiCalls.Average(s => s.ResponseTimeMs),
            ApiCalls: apiCalls.Count,
            CacheCalls: samples.Count(s => s.CacheUsed),
            TimedOut: samples.Count(s => s.TimedOut),
            Errors: errors,
            ErrorRate: apiCalls.Count == 0 ? 0 : (double)errors / apiCalls.Count);
    }

    private static ApiStats ApiStatsFor(string callName, DateTimeOffset timestamp, IReadOnlyList<ApiCallSample> samples)
    {
        var errors = samples.Count(s => s.ErrorCode != ErrorCode.Success);
        return new ApiStats(
            callName,
            timestamp,
            AverageResponseTime: samples.Average(s => s.ResponseTimeMs),
            Calls: samples.Count,
            Errors: errors,
            ErrorRate: (double)errors / samples.Count,
            ErrorCodes: samples
                .Where(s => s.ErrorCode != ErrorCode.Success)
                .GroupBy(s => s.ErrorCode)
                .ToDictionary(g => g.Key, g => g.Count()));
    }

    private void CheckClientAnomalies(ClientStats current, IReadOnlyList<ClientStats> past)
    {
        var pastCalls = past.Sum(p => p.ApiCalls);
        if (current.ApiCalls == 0 || pastCalls == 0)
            return;

        var baselineTime = past.Sum(p => p.AverageResponseTime * p.ApiCalls) / pastCalls;
        var baselineErrorRate = (double)past.Sum(p => p.Errors) / pastCalls;

        if (current.AverageResponseTime > baselineTime * Threshold)
            logger.LogWarning("{Client} average response time {Current:F0} ms is over 50% above the usual {Baseline:F0} ms",
                current.ClientName, current.AverageResponseTime, baselineTime);
        if (current.ErrorRate > baselineErrorRate * Threshold)
            logger.LogWarning("{Client} error rate {Current:P1} is over 50% above the usual {Baseline:P1}",
                current.ClientName, current.ErrorRate, baselineErrorRate);
    }

    private void CheckApiAnomalies(ApiStats current, IReadOnlyList<ApiStats> past)
    {
        var pastCalls = past.Sum(p => p.Calls);
        if (pastCalls == 0)
            return;

        var baselineTime = past.Sum(p => p.AverageResponseTime * p.Calls) / pastCalls;
        var baselineErrorRate = (double)past.Sum(p => p.Errors) / pastCalls;

        if (current.AverageResponseTime > baselineTime * Threshold)
            logger.LogWarning("{Call} average response time {Current:F0} ms is over 50% above the usual {Baseline:F0} ms",
                current.CallName, current.AverageResponseTime, baselineTime);
        if (current.ErrorRate > baselineErrorRate * Threshold)
            logger.LogWarning("{Call} error rate {Current:P1} is over 50% above the usual {Baseline:P1}",
                current.CallName, current.ErrorRate, baselineErrorRate);
    }
}
