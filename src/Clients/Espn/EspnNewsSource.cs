using UclAggregator.Models;
using UclAggregator.Services;

namespace UclAggregator.Clients.Espn;

public sealed class EspnNewsSource(EspnClient espn, FootballDataService football) : INewsSource
{
    public string Name => "espn";

    public bool AppliesTo(NewsQuery query) => query.Team is not null;

    public async Task<IReadOnlyList<SourceArticle>> SearchAsync(NewsQuery query, CancellationToken ct)
    {
        var team = await football.ResolveTeamAsync(query.Team!, ct);
        if (team?.EspnId is null)
        {
            return [];
        }

        var body = await espn.GetNewsAsync(team.EspnId, ct);
        return EspnMapper.MapNews(body);
    }
}
