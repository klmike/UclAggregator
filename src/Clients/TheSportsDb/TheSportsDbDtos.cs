using System.Text.Json.Serialization;

namespace UclAggregator.Clients.TheSportsDb;

public sealed record SportsDbTeams(List<SportsDbTeam>? Teams);

public sealed record SportsDbTeam(
    [property: JsonPropertyName("strTeam")] string? Name,
    [property: JsonPropertyName("idESPN")] string? EspnId);
