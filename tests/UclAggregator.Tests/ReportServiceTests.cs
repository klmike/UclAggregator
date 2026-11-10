using UclAggregator.Errors;
using UclAggregator.Models;
using UclAggregator.Tests.Support;
using Xunit;

namespace UclAggregator.Tests;

public class ReportServiceTests
{
    [Fact]
    public async Task Overview_joins_default_keywords_and_combines_news()
    {
        var google = new FakeNewsSource
        {
            Name = "google",
            Articles = [new SourceArticle { Id = "1", Title = "CL", Url = "https://n.test/1", PublishedAt = DateTimeOffset.UtcNow }],
        };
        var reports = TestServices.Reports(
            TestServices.Football(EspnJson.Route),
            TestServices.News(google),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("unused"))));

        var result = await reports.GetOverviewAsync("final", null, null, 5, CancellationToken.None);

        Assert.Equal("Champions League final", result.Keywords);
        Assert.Equal("1", Assert.Single(result.News).Id);
        Assert.Equal("Champions League final", google.Queries[0].Terms);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Overview_blank_keywords_uses_default_only(string? keywords)
    {
        var google = new FakeNewsSource { Name = "google" };
        var reports = TestServices.Reports(
            TestServices.Football(EspnJson.Route),
            TestServices.News(google),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("unused"))));

        var result = await reports.GetOverviewAsync(keywords, null, null, 5, CancellationToken.None);

        Assert.Equal("Champions League", result.Keywords);
        Assert.Equal("Champions League", Assert.Single(google.Queries).Terms);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Overview_empty_news_when_providers_fail()
    {
        var response = TestServices.Reports(
            TestServices.Football(EspnJson.Route),
            TestServices.News(new FakeNewsSource { Name = "guardian", Fail = new HttpRequestException("down") }),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("unused"))));

        var result = await response.GetOverviewAsync("final", null, null, 5, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Empty(result.News);
        Assert.Equal(ErrorCode.ProviderUnavailable, Assert.Single(result.Warnings).Code);
    }

    [Fact]
    public async Task Overview_limit_zero_returns_no_articles()
    {
        var google = new FakeNewsSource
        {
            Name = "google",
            Articles = [new SourceArticle { Id = "1", Title = "CL", PublishedAt = DateTimeOffset.UtcNow }],
        };
        var reports = TestServices.Reports(
            TestServices.Football(EspnJson.Route),
            TestServices.News(google),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("unused"))));

        var result = await reports.GetOverviewAsync("final", null, null, 0, CancellationToken.None);
        Assert.Empty(result.News);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Team_not_found_sets_error()
    {
        var reports = Reports(sportsDb: _ => TestHttp.Json("""{"teams":[]}"""));
        var result = await reports.GetTeamAsync("Nope", 2024, 5, CancellationToken.None);
        Assert.Equal(ErrorCode.TeamNotFound, result.Error!.Code);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Team_keeps_payload_when_standing_fails()
    {
        var reports = Reports(espn: request => TestHttp.Has(request, "standings") ? TestHttp.Fail() : EspnJson.Route(request));
        var result = await reports.GetTeamAsync("Arsenal", 2024, 5, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("Arsenal", result.Team);
        Assert.Equal(2024, result.Season);
        Assert.Null(result.Standing);
        Assert.NotEmpty(result.Matches);
        Assert.NotEmpty(result.TopPlayers);
        Assert.Equal(ErrorCode.ProviderUnavailable, Assert.Single(result.Warnings).Code);
    }

    [Fact]
    public async Task Team_keeps_payload_when_standing_times_out()
    {
        var reports = Reports(espn: request =>
        {
            if (TestHttp.Has(request, "standings"))
                throw new TaskCanceledException("timeout", new TimeoutException());
            return EspnJson.Route(request);
        });

        var result = await reports.GetTeamAsync("Arsenal", 2024, 5, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("Arsenal", result.Team);
        Assert.Null(result.Standing);
        Assert.NotEmpty(result.Matches);
        Assert.Equal(ErrorCode.ProviderTimeout, Assert.Single(result.Warnings).Code);
    }

    [Fact]
    public async Task Team_survives_when_espn_and_news_fail()
    {
        var news = new FakeNewsSource { Name = "guardian", Fail = new HttpRequestException("down") };
        var reports = TestServices.Reports(
            TestServices.Football(_ => TestHttp.Fail()),
            TestServices.News(news),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("unused"))));

        var result = await reports.GetTeamAsync("Arsenal", 2024, 5, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("Arsenal", result.Team);
        Assert.Empty(result.Matches);
        Assert.Null(result.Standing);
        Assert.Empty(result.TopPlayers);
        Assert.Empty(result.News);
        Assert.Contains(result.Warnings, w => w.Code == ErrorCode.ProviderUnavailable);
        Assert.True(result.Warnings.Count >= 3);
    }

    [Fact]
    public async Task Match_not_found_sets_error()
    {
        var reports = Reports(espn: _ => TestHttp.Json("""{"events":[]}"""));
        var byTeams = await reports.FindMatchAsync("Arsenal", "Madrid", new DateOnly(2024, 5, 1), 5, CancellationToken.None);
        var byId = await reports.GetMatchByIdAsync("missing", 5, CancellationToken.None);

        Assert.Equal(ErrorCode.MatchNotFound, byTeams.Error!.Code);
        Assert.Equal(ErrorCode.MatchNotFound, byId.Error!.Code);
    }

    [Fact]
    public async Task Match_includes_news_when_weather_is_skipped()
    {
        var news = new FakeNewsSource
        {
            Name = "google",
            Articles = [new SourceArticle { Id = "n1", Title = "Match", Url = "https://n.test/1", PublishedAt = DateTimeOffset.UtcNow }],
        };
        var reports = TestServices.Reports(
            TestServices.Football(EspnJson.Route),
            TestServices.News(news),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("down"))));

        var result = await reports.GetMatchByIdAsync("e1", 5, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("e1", result.Match!.Id);
        Assert.Null(result.Weather);
        Assert.Equal("n1", Assert.Single(result.News).Id);
        Assert.Contains(result.Warnings, w => w.Code == ErrorCode.DataNotAvailable);
    }

    [Fact]
    public async Task Match_survives_when_stats_and_news_fail()
    {
        var news = new FakeNewsSource { Name = "guardian", Fail = new TimeoutException() };
        var reports = TestServices.Reports(
            TestServices.Football(request => TestHttp.Has(request, "summary") ? TestHttp.Fail() : EspnJson.Route(request)),
            TestServices.News(news),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("down"))));

        var result = await reports.FindMatchAsync("Arsenal", "Madrid", new DateOnly(2024, 5, 1), 5, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("e1", result.Match!.Id);
        Assert.Null(result.Stats);
        Assert.Null(result.Weather);
        Assert.Empty(result.News);
        Assert.Contains(result.Warnings, w => w.Code == ErrorCode.ProviderUnavailable);
        Assert.Contains(result.Warnings, w => w.Code == ErrorCode.ProviderTimeout);
        Assert.Contains(result.Warnings, w => w.Code == ErrorCode.DataNotAvailable);
    }

    [Fact]
    public async Task Match_empty_summary_is_not_found()
    {
        var reports = Reports(espn: _ => TestHttp.Json("{}"));
        var result = await reports.GetMatchByIdAsync("e1", 5, CancellationToken.None);
        Assert.Equal(ErrorCode.MatchNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Player_not_found_sets_error()
    {
        var missingTeam = await Reports(sportsDb: _ => TestHttp.Json("""{"teams":[]}""")).GetPlayerAsync("Saka", "Nope", 2024, 5, CancellationToken.None);
        var missingPlayer = await Reports().GetPlayerAsync("Nobody", "Arsenal", 2024, 5, CancellationToken.None);

        Assert.Equal(ErrorCode.TeamNotFound, missingTeam.Error!.Code);
        Assert.Equal(ErrorCode.PlayerNotFound, missingPlayer.Error!.Code);
    }

    [Fact]
    public async Task Player_returns_stats_and_news()
    {
        var news = new FakeNewsSource
        {
            Name = "google",
            Articles = [new SourceArticle { Id = "p", Title = "Saka", Url = "https://n.test/p", PublishedAt = DateTimeOffset.UtcNow }],
        };
        var reports = TestServices.Reports(
            TestServices.Football(EspnJson.Route),
            TestServices.News(news),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("unused"))));

        var result = await reports.GetPlayerAsync("saka", "Arsenal", 2024, 5, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.Equal("Bukayo Saka", result.Player!.Name);
        Assert.Equal("p", Assert.Single(result.News).Id);
    }

    private static UclAggregator.Services.ReportService Reports(
        Func<HttpRequestMessage, HttpResponseMessage>? espn = null,
        Func<HttpRequestMessage, HttpResponseMessage>? sportsDb = null) =>
        TestServices.Reports(
            TestServices.Football(espn ?? EspnJson.Route, sportsDb),
            TestServices.News(new FakeNewsSource { Name = "google" }),
            TestServices.Weather(TestHttp.Throwing(new HttpRequestException("unused"))));
}
