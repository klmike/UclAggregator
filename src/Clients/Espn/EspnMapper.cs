using UclAggregator.Models;

namespace UclAggregator.Clients.Espn;

internal static class EspnMapper
{
    public const string Source = "espn";

    public static IReadOnlyList<SourceArticle> MapNews(EspnNewsDto? news) =>
        (news?.Articles ?? []).Select(a =>
            new SourceArticle { Id = $"espn:news:{a.Id}", Title = a.Headline, Url = a.Links?.Web?.Href, PublishedAt = DateParsing.Parse(a.Published), Publisher = "ESPN" }).ToList();

    public static Match MapEvent(EspnEventDto ev, EspnEventDto.VenueDto? venueOverride = null)
    {
        var comp = ev.Competitions?.FirstOrDefault();
        var competitors = comp?.Competitors ?? [];
        EspnEventDto.CompetitorDto? Side(string s) => competitors.FirstOrDefault(c => c.HomeAway == s);
        var home = Side("home");
        var away = Side("away");
        var venue = venueOverride ?? comp?.Venue;

        return new Match
        {
            Id = ev.Id ?? comp?.Id,
            Source = Source,
            Date = DateParsing.Parse(comp?.Date ?? ev.Date),
            Home = home?.Team?.DisplayName,
            Away = away?.Team?.DisplayName,
            HomeScore = ParseScore(home?.Score),
            AwayScore = ParseScore(away?.Score),
            Status = comp?.Status?.Type?.Description ?? ev.Status?.Type?.Description,
            Venue = venue is null ? null : new Venue { Name = venue.FullName, City = venue.Address?.City, Country = venue.Address?.Country },
            Stage = ev.SeasonType?.Name,
        };
    }

    public static MatchDetail? MapSummary(EspnSummaryDto summary)
    {
        if (summary.Header is null)
        {
            return null;
        }

        var stats = (summary.Boxscore?.Teams ?? []).Select(t =>
        {
            var values = new Dictionary<string, string?>();
            foreach (var s in t.Statistics ?? [])
            {
                if (s.Name is { } n)
                {
                    values[n] = s.DisplayValue;
                }
            }
            return new TeamStatistics { Team = t.Team?.DisplayName, Stats = values };
        }).ToList();
        return new MatchDetail { Match = MapEvent(summary.Header, summary.GameInfo?.Venue), Stats = stats };
    }

    public static Standing? MapStanding(EspnStandingsDto? body, string? teamId)
    {
        foreach (var group in body?.Children ?? [])
        {
            foreach (var entry in group.Standings?.Entries ?? [])
            {
                if (entry.Team?.Id != teamId)
                {
                    continue;
                }
                var stats = entry.Stats ?? [];
                return new Standing
                {
                    Team = entry.Team?.DisplayName,
                    Group = group.Name,
                    Rank = StatInt(stats, "rank"),
                    Points = StatInt(stats, "points"),
                    Played = StatInt(stats, "gamesPlayed"),
                    Wins = StatInt(stats, "wins"),
                    Draws = StatInt(stats, "ties"),
                    Losses = StatInt(stats, "losses"),
                    GoalDifference = StatInt(stats, "pointDifferential"),
                    Note = entry.Note?.Description,
                };
            }
        }
        return null;
    }

    public static IReadOnlyList<PlayerStats> MapRoster(EspnRosterDto? roster, string team, int season) =>
        (roster?.Athletes ?? []).Select(a =>
        {
            var stats = (a.Statistics?.Splits?.Categories ?? []).SelectMany(c => c.Stats ?? []).ToList();
            return new PlayerStats
            {
                Id = a.Id,
                Name = a.DisplayName,
                Team = team,
                Season = season,
                Position = a.Position?.DisplayName,
                Appearances = Stat(stats, "appearances") ?? 0,
                Goals = Stat(stats, "totalGoals") ?? 0,
                Assists = Stat(stats, "goalAssists") ?? 0,
            };
        }).ToList();

    private static int? ParseScore(string? score) => int.TryParse(score, out var value) ? value : null;

    private static int? StatInt(List<EspnStandingsDto.StatDto> stats, string name) =>
        stats.FirstOrDefault(s => s.Name == name)?.Value is { } v ? (int)v : null;

    private static int? Stat(List<EspnRosterDto.StatDto> stats, string name) =>
        stats.FirstOrDefault(s => s.Name == name)?.Value is { } v ? (int)v : null;
}
