using System.Text.Json;
using UclAggregator.Clients;
using UclAggregator.Clients.Espn;
using Xunit;

namespace UclAggregator.Tests;

public class EspnMapperTests
{
    [Fact]
    public void MapEvent_reads_sides_scores_and_venue()
    {
        var ev = JsonSerializer.Deserialize<EspnEventDto>(Support.EspnJson.Event, ApiJson.Options)!;
        var match = EspnMapper.MapEvent(ev);

        Assert.Equal("e1", match.Id);
        Assert.Equal("espn", match.Source);
        Assert.Equal("Arsenal", match.Home);
        Assert.Equal("Real Madrid", match.Away);
        Assert.Equal(2, match.HomeScore);
        Assert.Equal(1, match.AwayScore);
        Assert.Equal("FT", match.Status);
        Assert.Equal("Wembley", match.Venue?.Name);
        Assert.Equal("London", match.Venue?.City);
        Assert.Equal("Final", match.Stage);
    }

    [Fact]
    public void MapEvent_reads_score_object()
    {
        var json = """
            {"id":"e1","competitions":[{"competitors":[
              {"homeAway":"home","team":{"displayName":"Home"},"score":{"displayValue":"3"}},
              {"homeAway":"away","team":{"displayName":"Away"},"score":"0"}
            ]}]}
            """;
        var match = EspnMapper.MapEvent(JsonSerializer.Deserialize<EspnEventDto>(json, ApiJson.Options)!);
        Assert.Equal(3, match.HomeScore);
        Assert.Equal(0, match.AwayScore);
    }

    [Fact]
    public void MapStanding_finds_team_and_stats()
    {
        var body = JsonSerializer.Deserialize<EspnStandingsDto>(Support.EspnJson.Standings, ApiJson.Options);
        var standing = EspnMapper.MapStanding(body, "359");

        Assert.Equal("Arsenal", standing!.Team);
        Assert.Equal(1, standing.Rank);
        Assert.Equal(5, standing.Wins);
        Assert.Equal("Qualified", standing.Note);
        Assert.Null(EspnMapper.MapStanding(body, "missing"));
    }

    [Fact]
    public void MapRoster_reads_player_totals()
    {
        var roster = JsonSerializer.Deserialize<EspnRosterDto>(Support.EspnJson.Roster, ApiJson.Options);
        var saka = EspnMapper.MapRoster(roster, "Arsenal", 2024).First();

        Assert.Equal("Bukayo Saka", saka.Name);
        Assert.Equal(10, saka.Appearances);
        Assert.Equal(5, saka.Goals);
        Assert.Equal(3, saka.Assists);
        Assert.Equal("Arsenal", saka.Team);
        Assert.Equal(2024, saka.Season);
    }

    [Fact]
    public void MapSummary_null_without_header()
    {
        Assert.Null(EspnMapper.MapSummary(new EspnSummaryDto()));
    }
}
