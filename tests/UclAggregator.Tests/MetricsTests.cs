using UclAggregator.Errors;
using UclAggregator.Metrics;
using UclAggregator.Metrics.Api;
using UclAggregator.Metrics.Clients;
using Xunit;

namespace UclAggregator.Tests;

public class MetricsTests
{
    private static ApiCallSample Api(string name, double ms, ErrorCode code = ErrorCode.Success, TimeSpan? age = null) =>
        new(name, DateTimeOffset.UtcNow - (age ?? TimeSpan.Zero), ms, code);

    private static ClientCallSample Client(string name, double ms, bool timedOut = false, TimeSpan? age = null) =>
        new(name, DateTimeOffset.UtcNow - (age ?? TimeSpan.Zero), ms, ApiCall: true, CacheUsed: false, TimedOut: timedOut, ClientError: false);

    [Fact]
    public void SampleBuffer_Drain_ReturnsPendingOnce_ButRecentKeepsSamples()
    {
        var buffer = new SampleBuffer<ApiCallSample>();
        buffer.Record(Api("a", 10));
        buffer.Record(Api("a", 20));

        Assert.Equal(2, buffer.Drain().Count);
        Assert.Empty(buffer.Drain());
        Assert.Equal(2, buffer.Recent().Count);
    }

    [Fact]
    public void SampleBuffer_Recent_ExcludesSamplesOlderThanWindow()
    {
        var buffer = new SampleBuffer<ApiCallSample>();
        buffer.Record(Api("a", 10, age: TimeSpan.FromMinutes(6)));
        buffer.Record(Api("a", 20));

        var recent = buffer.Recent();

        Assert.Single(recent);
        Assert.Equal(20, recent[0].ResponseTimeMs);
    }

    [Fact]
    public void ApiStats_From_ComputesAverageErrorsAndCodes()
    {
        var stats = ApiStats.From("a", DateTimeOffset.UtcNow,
            [Api("a", 100), Api("a", 300, ErrorCode.TeamNotFound), Api("a", 200, ErrorCode.TeamNotFound)]);

        Assert.Equal(3, stats.Calls);
        Assert.Equal(200, stats.AverageResponseTime);
        Assert.Equal(2, stats.Errors);
        Assert.Equal(2d / 3, stats.ErrorRate, 5);
        Assert.Equal(2, stats.ErrorCodes[ErrorCode.TeamNotFound]);
    }

    [Fact]
    public void ClientStats_From_CountsTimeoutsAsErrors()
    {
        var stats = ClientStats.From("c", DateTimeOffset.UtcNow, [Client("c", 100), Client("c", 300, timedOut: true)]);

        Assert.Equal(2, stats.ApiCalls);
        Assert.Equal(200, stats.AverageResponseTime);
        Assert.Equal(1, stats.TimedOut);
        Assert.Equal(1, stats.Errors);
        Assert.Equal(0.5, stats.ErrorRate);
    }

    [Fact]
    public void StatsHistory_KeepsPerKeyHistory_AndCapsEntries()
    {
        var history = new StatsHistory<int>();
        for (var i = 0; i < 2020; i++)
            history.Add("k", i);
        history.Add("other", 1);

        var list = history.Get("k");
        Assert.Equal(2016, list.Count);
        Assert.Equal(4, list[0]);
        Assert.Empty(history.Get("missing"));
        Assert.Equal(2, history.GetAll().Count);
    }

    [Fact]
    public void ApiMetricsResponse_LatestIsLive_WithoutBackgroundJob()
    {
        var samples = new SampleBuffer<ApiCallSample>();
        var history = new StatsHistory<ApiStats>();
        samples.Record(Api("fast", 10));
        samples.Record(Api("slow", 500));

        var response = ApiMetricsResponse.From(history, samples);

        Assert.Equal("fast", Assert.Single(response.Latest.Fast).CallName);
        Assert.Equal("slow", Assert.Single(response.Latest.Slow).CallName);
        Assert.Empty(response.History.Fast);
    }

    [Fact]
    public void ApiMetricsResponse_HistoryHoldsOnlyStoredWindows()
    {
        var samples = new SampleBuffer<ApiCallSample>();
        var history = new StatsHistory<ApiStats>();
        history.Add("a", ApiStats.From("a", DateTimeOffset.UtcNow.AddMinutes(-5), [Api("a", 150)]));
        samples.Record(Api("a", 20));

        var response = ApiMetricsResponse.From(history, samples);

        Assert.Equal(20, Assert.Single(response.Latest.Fast).AverageResponseTime);
        // history follows the bucket chosen by the live window
        Assert.Equal(150, Assert.Single(response.History.Fast).AverageResponseTime);
        Assert.Empty(response.History.Average);
    }

    [Fact]
    public void ApiMetricsResponse_NoRecentCalls_FallsBackToHistoryAverageForBucket()
    {
        var samples = new SampleBuffer<ApiCallSample>();
        var history = new StatsHistory<ApiStats>();
        history.Add("a", ApiStats.From("a", DateTimeOffset.UtcNow, [Api("a", 150)]));

        var response = ApiMetricsResponse.From(history, samples);

        Assert.Empty(response.Latest.Average);
        Assert.Single(response.History.Average);
    }

    [Fact]
    public void ClientMetricsResponse_LatestIsLive()
    {
        var samples = new SampleBuffer<ClientCallSample>();
        samples.Record(Client("c", 250));

        var response = ClientMetricsResponse.From(new StatsHistory<ClientStats>(), samples);

        Assert.Equal("c", Assert.Single(response.Latest.Slow).ClientName);
    }
}
