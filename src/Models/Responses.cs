namespace UclAggregator.Models;

public class OverviewResponse : ApiResponse
{
    public string Keywords { get; set; } = "";
    public IReadOnlyList<NewsArticle> News { get; set; } = [];
}

public class TeamResponse : ApiResponse
{
    public string Team { get; set; } = "";
    public int Season { get; set; }
    public Standing? Standing { get; set; }
    public IReadOnlyList<Match> Matches { get; set; } = [];
    public IReadOnlyList<PlayerStats> TopPlayers { get; set; } = [];
    public IReadOnlyList<NewsArticle> News { get; set; } = [];
}

public class MatchResponse : ApiResponse
{
    public Match? Match { get; set; }
    public IReadOnlyList<TeamStatistics>? Stats { get; set; }
    public Weather? Weather { get; set; }
    public IReadOnlyList<NewsArticle> News { get; set; } = [];
}

public class PlayerResponse : ApiResponse
{
    public string Team { get; set; } = "";
    public int Season { get; set; }
    public PlayerStats? Player { get; set; }
    public IReadOnlyList<NewsArticle> News { get; set; } = [];
}
