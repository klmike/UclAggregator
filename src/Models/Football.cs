using System.Text.Json.Serialization;

namespace UclAggregator.Models;

public class Venue
{
    public string? Name { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
}

public class Match
{
    public string? Id { get; set; }
    public required string Source { get; set; }
    public DateTimeOffset? Date { get; set; }
    public string? Home { get; set; }
    public string? Away { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string? Status { get; set; }
    public Venue? Venue { get; set; }
    public string? Stage { get; set; }

    [JsonIgnore]
    public string Title => $"{Home} vs {Away}";
}

public class Standing
{
    public string? Team { get; set; }
    public string? Group { get; set; }
    public int? Rank { get; set; }
    public int? Points { get; set; }
    public int? Played { get; set; }
    public int? Wins { get; set; }
    public int? Draws { get; set; }
    public int? Losses { get; set; }
    public int? GoalDifference { get; set; }
    public string? Note { get; set; }
}

public class PlayerStats
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public required string Team { get; set; }
    public required int Season { get; set; }
    public string? Position { get; set; }
    public required int Appearances { get; set; }
    public required int Goals { get; set; }
    public required int Assists { get; set; }
}

public class TeamStatistics
{
    public string? Team { get; set; }
    public required IReadOnlyDictionary<string, string?> Stats { get; set; }
}

internal class MatchDetail
{
    public required Match Match { get; set; }
    public required IReadOnlyList<TeamStatistics> Stats { get; set; }
}
