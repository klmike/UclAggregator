namespace UclAggregator.Clients.TheSportsDb;

public sealed class TheSportsDbClient(HttpClient http)
{
    public Task<SportsDbTeams?> SearchTeamAsync(string name, CancellationToken ct) =>
        http.GetFromJsonAsync<SportsDbTeams>($"searchteams.php?t={Uri.EscapeDataString(name)}", ApiJson.Options, ct);
}
