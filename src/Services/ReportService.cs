using UclAggregator.Errors;
using Microsoft.Extensions.Options;
using UclAggregator.Models;
using UclAggregator.Options;

namespace UclAggregator.Services;

public sealed class ReportService(
    FootballDataService football,
    NewsService news,
    WeatherService weather,
    IOptions<AggregatorOptions> options,
    ILogger<ReportService> logger)
{
    private readonly AggregatorOptions _opt = options.Value;

    public async Task<OverviewResponse> GetOverviewAsync(string? keywords, DateOnly? from, DateOnly? to, int? limit, CancellationToken ct)
    {
        var terms = string.Join(' ', new[] { _opt.DefaultKeywords, keywords }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var response = new OverviewResponse { Keywords = terms };
        var found = await news.SearchAsync(terms, from, to, null, response, ct);
        response.News = NewsService.Combine(found, limit ?? _opt.DefaultNewsLimit);
        return response;
    }

    public async Task<TeamResponse> GetTeamAsync(string name, int? season, int? limit, CancellationToken ct)
    {
        var response = new TeamResponse();
        var team = await football.ResolveTeamAsync(name, ct);
        if (team is null)
        {
            response.AddError(ErrorCode.TeamNotFound, $"Team '{name}' was not found.");
            return response;
        }

        var year = season ?? CurrentSeason();
        var matches = TryAsync(response, "Matches", () => football.GetTeamMatchesAsync(team, year, ct));
        var standing = TryAsync(response, "Standing", () => football.GetStandingAsync(team, year, ct));
        var players = TryAsync(response, "Players", () => football.GetTopPlayersAsync(team, year, _opt.TopPlayersWithNews, ct));
        var teamNews = news.SearchAsync(team.Name, null, null, team.Name, response, ct);
        await Task.WhenAll(matches, standing, players, teamNews);

        var top = players.Result ?? [];
        var playerNews = await Task.WhenAll(top.Select(p => news.SearchAsync($"{p.Name} {team.Name}", null, null, null, response, ct)));

        response.Team = team.Name;
        response.Season = year;
        response.Standing = standing.Result;
        response.Matches = (matches.Result ?? []).OrderBy(m => m.Date).ToList();
        response.TopPlayers = top;
        response.News = NewsService.Combine([teamNews.Result, .. playerNews], limit ?? _opt.DefaultNewsLimit);
        return response;
    }

    public async Task<MatchResponse> GetMatchByIdAsync(string id, int? limit, CancellationToken ct)
    {
        var response = new MatchResponse();
        var detail = await football.GetMatchDetailAsync(id, ct);
        if (detail is null)
        {
            response.AddError(ErrorCode.MatchNotFound, $"Match '{id}' was not found.");
            return response;
        }

        await BuildMatchAsync(response, detail.Match, detail.Stats, limit, ct);
        return response;
    }

    public async Task<MatchResponse> FindMatchAsync(string home, string away, DateOnly? date, int? limit, CancellationToken ct)
    {
        var response = new MatchResponse();
        var match = await football.FindMatchAsync(home, away, date, CurrentSeason(), ct);
        if (match is null)
        {
            response.AddError(ErrorCode.MatchNotFound, $"No match found for '{home}' vs '{away}'.");
            return response;
        }

        MatchDetail? detail = null;
        if (date is not null && match.Id is { } id)
            detail = await TryAsync(response, "Match stats", () => football.GetMatchDetailAsync(id, ct));
        await BuildMatchAsync(response, detail?.Match ?? match, detail?.Stats, limit, ct);
        return response;
    }

    public async Task<PlayerResponse> GetPlayerAsync(string name, string team, int? season, int? limit, CancellationToken ct)
    {
        var response = new PlayerResponse();
        var teamRef = await football.ResolveTeamAsync(team, ct);
        if (teamRef is null)
        {
            response.AddError(ErrorCode.TeamNotFound, $"Team '{team}' was not found.");
            return response;
        }

        var year = season ?? CurrentSeason();
        var player = await football.FindPlayerAsync(teamRef, year, name, ct);
        if (player is null)
        {
            response.AddError(ErrorCode.PlayerNotFound, $"Player '{name}' was not found in '{team}'.");
            return response;
        }

        var found = await news.SearchAsync($"{player.Name} {teamRef.Name}", null, null, null, response, ct);
        response.Team = teamRef.Name;
        response.Season = year;
        response.Player = player;
        response.News = NewsService.Combine(found, limit ?? _opt.DefaultNewsLimit);
        return response;
    }

    private async Task BuildMatchAsync(
        MatchResponse response, Match match, IReadOnlyList<TeamStatistics>? stats, int? limit, CancellationToken ct)
    {
        var terms = $"{_opt.DefaultKeywords} {match.Title}";
        var day = match.Date is { } t ? DateOnly.FromDateTime(t.UtcDateTime) : (DateOnly?)null;

        var weatherTask = weather.GetForMatchAsync(match, response, ct);
        var newsTask = news.SearchAsync(terms, day?.AddDays(-2), day?.AddDays(2), null, response, ct);
        await Task.WhenAll(weatherTask, newsTask);

        response.Match = match;
        response.Stats = stats;
        response.Weather = weatherTask.Result;
        response.News = NewsService.Combine(newsTask.Result, limit ?? _opt.DefaultNewsLimit);
    }

    private async Task<T?> TryAsync<T>(ApiResponse response, string what, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = ApiError.From(ex, what);
            logger.LogWarning(ex, "{What} failed with {Code}", what, error.Code);
            response.AddWarning(error);
            return default;
        }
    }
    private static int CurrentSeason()
    {
        var now = DateTime.UtcNow;
        return now.Month >= 7 ? now.Year : now.Year - 1;
    }
}
