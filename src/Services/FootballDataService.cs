using UclAggregator.Clients.Espn;
using UclAggregator.Clients.TheSportsDb;
using UclAggregator.Models;

namespace UclAggregator.Services;

public sealed record TeamRef(string Name, string? EspnId);

public sealed class FootballDataService(
    EspnClient espn,
    TheSportsDbClient sportsDb,
    ILogger<FootballDataService> logger)
{
    public async Task<TeamRef?> ResolveTeamAsync(string name, CancellationToken ct)
    {
        try
        {
            if (await ResolveTeamFromEspnAsync(name, ct) is { } team)
            {
                return team;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "ESPN lookup failed for {Name}; falling back to TheSportsDB", name);
            return await ResolveTeamFromSportsDbAsync(name, ct) ?? throw new HttpRequestException("ESPN team lookup failed", ex);
        }
        return await ResolveTeamFromSportsDbAsync(name, ct);
    }

    private async Task<TeamRef?> ResolveTeamFromSportsDbAsync(string name, CancellationToken ct)
    {
        var t = (await sportsDb.SearchTeamAsync(name, ct))?.Teams?.FirstOrDefault();
        if (t is null)
        {
            logger.LogWarning("TheSportsDB found no team for {Name}", name);
            return null;
        }
        if (t.EspnId is null)
        {
            logger.LogWarning("Team {Team} has no ESPN id; matches, standing and players will be empty", t.Name ?? name);
        }
        return new TeamRef(t.Name ?? name, t.EspnId);
    }

    private async Task<TeamRef?> ResolveTeamFromEspnAsync(string name, CancellationToken ct)
    {
        var teams = (await espn.GetTeamsAsync(ct))?.Sports?.FirstOrDefault()?.Leagues?.FirstOrDefault()?.Teams?
            .Select(e => e.Team).OfType<EspnTeamsDto.TeamDto>().Where(t => t.Id is not null).ToList() ?? [];
        var match = teams.FirstOrDefault(t => Names(t).Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
            ?? teams.FirstOrDefault(t => Names(t).Any(n => n.Contains(name, StringComparison.OrdinalIgnoreCase)));
        return match is null ? null : new TeamRef(match.DisplayName ?? name, match.Id);

        static IEnumerable<string> Names(EspnTeamsDto.TeamDto t) =>
            new[] { t.DisplayName, t.ShortDisplayName, t.Name, t.Location }.OfType<string>();
    }

    internal async Task<MatchDetail?> GetMatchDetailAsync(string id, CancellationToken ct) =>
        await espn.GetSummaryAsync(id, ct) is { } summary ? EspnMapper.MapSummary(summary) : null;

    public async Task<Match?> FindMatchAsync(string home, string away, DateOnly? date, int season, CancellationToken ct)
    {
        if (date is { } day)
        {
            return (await GetScoreboardAsync(day, ct)).FirstOrDefault(m => Mentions(m, home) && Mentions(m, away));
        }

        var team = await ResolveTeamAsync(home, ct);
        if (team is null)
        {
            return null;
        }
        var now = DateTimeOffset.UtcNow;
        return (await GetTeamMatchesAsync(team, season, ct))
            .Where(m => Mentions(m, away) && m.Date is not null)
            .OrderBy(m => Math.Abs((m.Date!.Value - now).Ticks))
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<Match>> GetTeamMatchesAsync(TeamRef team, int season, CancellationToken ct)
    {
        if (team.EspnId is null)
        {
            return [];
        }
        var schedule = await espn.GetScheduleAsync(team.EspnId, season, ct);
        return (schedule?.Events ?? []).Select(e => EspnMapper.MapEvent(e)).ToList();
    }

    public async Task<Standing?> GetStandingAsync(TeamRef team, int season, CancellationToken ct) =>
        EspnMapper.MapStanding(await espn.GetStandingsAsync(season, ct), team.EspnId);

    public async Task<IReadOnlyList<PlayerStats>> GetTopPlayersAsync(TeamRef team, int season, int count, CancellationToken ct) =>
        (await GetPlayersAsync(team, season, ct))
            .OrderByDescending(p => p.Goals)
            .ThenByDescending(p => p.Assists)
            .Take(count)
            .ToList();

    public async Task<PlayerStats?> FindPlayerAsync(TeamRef team, int season, string name, CancellationToken ct) =>
        (await GetPlayersAsync(team, season, ct))
            .FirstOrDefault(p => p.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);

    private async Task<IReadOnlyList<PlayerStats>> GetPlayersAsync(TeamRef team, int season, CancellationToken ct) =>
        team.EspnId is null
            ? []
            : EspnMapper.MapRoster(await espn.GetRosterAsync(team.EspnId, season, ct), team.Name, season);

    private async Task<IReadOnlyList<Match>> GetScoreboardAsync(DateOnly day, CancellationToken ct) =>
        ((await espn.GetScoreboardAsync(day, ct))?.Events ?? []).Select(e => EspnMapper.MapEvent(e)).ToList();

    private static bool Mentions(Match m, string team) =>
        m.Home?.Contains(team, StringComparison.OrdinalIgnoreCase) == true
        || m.Away?.Contains(team, StringComparison.OrdinalIgnoreCase) == true;
}
