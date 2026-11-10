namespace UclAggregator.Tests.Support;

internal static class EspnJson
{
    public const string Event = """
        {
          "id": "e1",
          "date": "2024-05-01T19:00:00Z",
          "status": { "type": { "description": "FT" } },
          "seasonType": { "name": "Final" },
          "competitions": [{
            "id": "c1",
            "date": "2024-05-01T19:00:00Z",
            "competitors": [
              { "homeAway": "home", "team": { "displayName": "Arsenal" }, "score": "2" },
              { "homeAway": "away", "team": { "displayName": "Real Madrid" }, "score": "1" }
            ],
            "venue": { "fullName": "Wembley", "address": { "city": "London", "country": "England" } }
          }]
        }
        """;

    public static string Scoreboard => $$"""{ "events": [{{Event}}] }""";

    public static string Schedule => Scoreboard;

    public const string Standings = """
        {
          "children": [{
            "name": "Group A",
            "standings": {
              "entries": [{
                "team": { "id": "359", "displayName": "Arsenal" },
                "stats": [
                  { "name": "rank", "value": 1 },
                  { "name": "points", "value": 16 },
                  { "name": "gamesPlayed", "value": 6 },
                  { "name": "wins", "value": 5 },
                  { "name": "ties", "value": 1 },
                  { "name": "losses", "value": 0 },
                  { "name": "pointDifferential", "value": 10 }
                ],
                "note": { "description": "Qualified" }
              }]
            }
          }]
        }
        """;

    public const string Roster = """
        {
          "athletes": [
            {
              "id": "p1",
              "displayName": "Bukayo Saka",
              "position": { "displayName": "Forward" },
              "statistics": { "splits": { "categories": [{ "stats": [
                { "name": "appearances", "value": 10 },
                { "name": "totalGoals", "value": 5 },
                { "name": "goalAssists", "value": 3 }
              ] }] } }
            },
            {
              "id": "p2",
              "displayName": "Martin Odegaard",
              "position": { "displayName": "Midfielder" },
              "statistics": { "splits": { "categories": [{ "stats": [
                { "name": "appearances", "value": 9 },
                { "name": "totalGoals", "value": 5 },
                { "name": "goalAssists", "value": 4 }
              ] }] } }
            }
          ]
        }
        """;

    public static string Summary => $$"""
        {
          "header": {{Event}},
          "gameInfo": { "venue": { "fullName": "Wembley", "address": { "city": "London", "country": "England" } } },
          "boxscore": { "teams": [{ "team": { "displayName": "Arsenal" }, "statistics": [{ "name": "possessionPct", "displayValue": "58%" }] }] }
        }
        """;

    public static HttpResponseMessage Route(HttpRequestMessage request)
    {
        if (TestHttp.Has(request, "standings")) return TestHttp.Json(Standings);
        if (TestHttp.Has(request, "roster")) return TestHttp.Json(Roster);
        if (TestHttp.Has(request, "summary")) return TestHttp.Json(Summary);
        if (TestHttp.Has(request, "scoreboard") || TestHttp.Has(request, "schedule")) return TestHttp.Json(Scoreboard);
        if (TestHttp.Has(request, "news")) return TestHttp.Json("""{ "articles": [] }""");
        if (request.RequestUri!.AbsolutePath.EndsWith("/teams", StringComparison.Ordinal)) return TestHttp.Json(Teams);
        return TestHttp.Fail();
    }

    public const string Teams = """
        { "sports": [{ "leagues": [{ "teams": [{ "team": { "id": "359", "displayName": "Arsenal" } }] }] }] }
        """;
}
