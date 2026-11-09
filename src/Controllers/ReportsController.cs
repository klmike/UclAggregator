using Microsoft.AspNetCore.Mvc;
using UclAggregator.Models;
using UclAggregator.Services;

namespace UclAggregator.Controllers;

[ApiController]
[Route("")]
public class ReportsController(ReportService reports) : ApiControllerBase
{
    [HttpGet("news")]
    public async Task<ActionResult<OverviewResponse>> Overview(string? keywords, DateOnly? from, DateOnly? to, int? limit, CancellationToken ct) =>
        Respond(await reports.GetOverviewAsync(keywords?.Trim(), from, to, limit, ct));

    [HttpGet("teams/{name}")]
    public async Task<ActionResult<TeamResponse>> Team(string name, int? season, int? limit, CancellationToken ct) =>
        Respond(await reports.GetTeamAsync(name.Trim(), season, limit, ct));

    [HttpGet("matches/{id}")]
    public async Task<ActionResult<MatchResponse>> MatchById(string id, int? limit, CancellationToken ct) =>
        Respond(await reports.GetMatchByIdAsync(id, limit, ct));

    [HttpGet("matches")]
    public async Task<ActionResult<MatchResponse>> Match(string home, string away, DateOnly? date, int? limit, CancellationToken ct) =>
        Respond(await reports.FindMatchAsync(home.Trim(), away.Trim(), date, limit, ct));

    [HttpGet("players/{name}")]
    public async Task<ActionResult<PlayerResponse>> Player(string name, string team, int? season, int? limit, CancellationToken ct) =>
        Respond(await reports.GetPlayerAsync(name.Trim(), team.Trim(), season, limit, ct));
}
