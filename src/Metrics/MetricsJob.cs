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
    // 1.5 = a value more than 50% above its historical baseline is logged as an anomaly
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
            var stats = ClientStats.From(group.Key, now, group.ToList());
            Check(stats.ClientName, stats.AverageResponseTime, stats.ApiCalls, stats.Errors,
                clientHistory.Get(group.Key).Select(p => (p.AverageResponseTime, p.ApiCalls, p.Errors)));
            clientHistory.Add(stats.ClientName, stats);
        }
    }

    private void RunApi()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var group in apiSamples.Drain().GroupBy(s => s.CallName))
        {
            var stats = ApiStats.From(group.Key, now, group.ToList());
            Check(stats.CallName, stats.AverageResponseTime, stats.Calls, stats.Errors,
                apiHistory.Get(group.Key).Select(p => (p.AverageResponseTime, p.Calls, p.Errors)));
            apiHistory.Add(stats.CallName, stats);
        }
    }

    private void Check(
        string name,
        double currentAverage,
        int currentCalls,
        int currentErrors,
        IEnumerable<(double Average, int Calls, int Errors)> past)
    {
        var previous = past.ToList();
        var pastCalls = previous.Sum(p => p.Calls);
        if (currentCalls == 0 || pastCalls == 0)
            return;

        var baselineTime = previous.Sum(p => p.Average * p.Calls) / pastCalls;
        var baselineErrorRate = (double)previous.Sum(p => p.Errors) / pastCalls;

        if (currentAverage > baselineTime * Threshold)
            logger.LogWarning("{Name} average response time {Current:F0} ms is over 50% above the usual {Baseline:F0} ms",
                name, currentAverage, baselineTime);
        if ((double)currentErrors / currentCalls > baselineErrorRate * Threshold)
            logger.LogWarning("{Name} error rate {Current:P1} is over 50% above the usual {Baseline:P1}",
                name, (double)currentErrors / currentCalls, baselineErrorRate);
    }
}
