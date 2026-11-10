using UclAggregator.Services;
using UclAggregator.Tests.Support;
using Xunit;

namespace UclAggregator.Tests;

public class FootballDataServiceTests
{
    [Fact]
    public async Task Resolve_returns_null_when_team_missing()
    {
        var football = TestServices.Football(EspnJson.Route, _ => TestHttp.Json("""{"teams":null}"""));
        Assert.Null(await football.ResolveTeamAsync("Nope", CancellationToken.None));
    }

    [Fact]
    public async Task Resolve_prefers_espn_and_falls_back_to_sportsdb()
    {
        var viaEspn = TestServices.Football(EspnJson.Route, _ => TestHttp.Fail());
        Assert.Equal("359", (await viaEspn.ResolveTeamAsync("Arsenal", CancellationToken.None))!.EspnId);

        var viaSportsDb = TestServices.Football(_ => TestHttp.Fail());
        Assert.Equal("359", (await viaSportsDb.ResolveTeamAsync("Arsenal", CancellationToken.None))!.EspnId);
    }

    [Fact]
    public async Task Resolve_keeps_team_without_espn_id()
    {
        var football = TestServices.Football(
            _ => TestHttp.Fail(),
            _ => TestHttp.Json("""{"teams":[{"strTeam":"Arsenal","idESPN":null}]}"""));

        var team = await football.ResolveTeamAsync("Arsenal", CancellationToken.None);

        Assert.Equal("Arsenal", team!.Name);
        Assert.Null(team.EspnId);
        Assert.Empty(await football.GetTeamMatchesAsync(team, 2024, CancellationToken.None));
        Assert.Empty(await football.GetTopPlayersAsync(team, 2024, 3, CancellationToken.None));
    }

    [Fact]
    public async Task FindMatch_by_date_filters_home_and_away()
    {
        var football = TestServices.Football(EspnJson.Route);
        var match = await football.FindMatchAsync("arsenal", "madrid", new DateOnly(2024, 5, 1), 2024, CancellationToken.None);

        Assert.Equal("e1", match!.Id);
        Assert.Equal("Arsenal", match.Home);
        Assert.Equal("Real Madrid", match.Away);
        Assert.Equal(2, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
    }

    [Fact]
    public async Task FindMatch_without_date_picks_closest_opponent()
    {
        var football = TestServices.Football(EspnJson.Route);
        var match = await football.FindMatchAsync("Arsenal", "Madrid", date: null, 2024, CancellationToken.None);
        Assert.Equal("e1", match!.Id);
    }

    [Fact]
    public async Task FindMatch_returns_null_when_opponent_or_team_missing()
    {
        var football = TestServices.Football(EspnJson.Route);
        Assert.Null(await football.FindMatchAsync("Arsenal", "Barcelona", new DateOnly(2024, 5, 1), 2024, CancellationToken.None));

        var unknown = TestServices.Football(EspnJson.Route, _ => TestHttp.Json("""{"teams":[]}"""));
        Assert.Null(await unknown.FindMatchAsync("Nope", "Madrid", date: null, 2024, CancellationToken.None));
    }

    [Fact]
    public async Task Standing_and_players_map_from_espn()
    {
        var football = TestServices.Football(EspnJson.Route);
        var team = new TeamRef("Arsenal", "359");

        var standing = await football.GetStandingAsync(team, 2024, CancellationToken.None);
        var top = await football.GetTopPlayersAsync(team, 2024, 1, CancellationToken.None);
        var player = await football.FindPlayerAsync(team, 2024, "saka", CancellationToken.None);

        Assert.Equal(1, standing!.Rank);
        Assert.Equal(16, standing.Points);
        Assert.Equal("Group A", standing.Group);
        Assert.Equal("Martin Odegaard", Assert.Single(top).Name);
        Assert.Equal("Bukayo Saka", player!.Name);
        Assert.Equal(5, player.Goals);
    }
}
