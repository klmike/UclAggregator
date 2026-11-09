using Microsoft.Extensions.Options;
using UclAggregator.Options;

namespace UclAggregator.Clients.Espn;

public sealed class EspnClient(HttpClient http, IOptions<EspnOptions> options)
{
    private readonly EspnOptions _opt = options.Value;

    public Task<EspnScoreboardDto?> GetScoreboardAsync(DateOnly date, CancellationToken ct) =>
        Get<EspnScoreboardDto>($"scoreboard?dates={date:yyyyMMdd}", ct);

    public Task<EspnSummaryDto?> GetSummaryAsync(string matchId, CancellationToken ct) =>
        Get<EspnSummaryDto>($"summary?event={Uri.EscapeDataString(matchId)}", ct);

    public Task<EspnScheduleDto?> GetScheduleAsync(string teamId, int season, CancellationToken ct) =>
        Get<EspnScheduleDto>($"teams/{Uri.EscapeDataString(teamId)}/schedule?season={season}", ct);

    public Task<EspnRosterDto?> GetRosterAsync(string teamId, int season, CancellationToken ct) =>
        Get<EspnRosterDto>($"teams/{Uri.EscapeDataString(teamId)}/roster?season={season}", ct);

    public Task<EspnNewsDto?> GetNewsAsync(string teamId, CancellationToken ct) =>
        Get<EspnNewsDto>($"news?team={Uri.EscapeDataString(teamId)}", ct);

    public Task<EspnStandingsDto?> GetStandingsAsync(int season, CancellationToken ct) =>
        Get<EspnStandingsDto>($"{_opt.StandingsUrl}?season={season}", ct);

    private Task<T?> Get<T>(string url, CancellationToken ct) => http.GetFromJsonAsync<T>(url, ApiJson.Options, ct);
}
