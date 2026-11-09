using Microsoft.Extensions.Logging.Abstractions;
using UclAggregator.Clients;
using UclAggregator.Clients.Espn;
using UclAggregator.Clients.OpenMeteo;
using UclAggregator.Clients.TheSportsDb;
using UclAggregator.Options;
using UclAggregator.Services;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace UclAggregator.Tests.Support;

internal static class TestServices
{
    public static readonly AggregatorOptions Aggregator = new()
    {
        DefaultKeywords = "Champions League",
        DefaultNewsLimit = 20,
        NewsHalfLifeDays = 7,
        NewsFallbackMaxArticles = 1,
        TopPlayersWithNews = 3,
    };

    public static NewsService News(params INewsSource[] sources) =>
        new(sources, MsOptions.Create(Aggregator), NullLogger<NewsService>.Instance);

    public static WeatherService Weather(HttpClient http) =>
        new(new OpenMeteoClient(http, MsOptions.Create(new OpenMeteoOptions
        {
            GeocodingUrl = "https://geocode.test/search",
            ForecastUrl = "https://forecast.test/forecast",
            ArchiveUrl = "https://archive.test/archive",
            Hourly = "temperature_2m",
            GeocodeCount = 10,
            ForecastPastDays = 90,
        })), MsOptions.Create(new OpenMeteoOptions { ForecastPastDays = 90 }), NullLogger<WeatherService>.Instance);

    public static FootballDataService Football(
        Func<HttpRequestMessage, HttpResponseMessage> espn,
        Func<HttpRequestMessage, HttpResponseMessage>? sportsDb = null)
    {
        var espnClient = new EspnClient(
            TestHttp.Client(espn, "https://espn.test/"),
            MsOptions.Create(new EspnOptions { StandingsUrl = "https://espn.test/standings" }));
        var sports = new TheSportsDbClient(TestHttp.Client(
            sportsDb ?? (_ => TestHttp.Json("""{"teams":[{"strTeam":"Arsenal","idESPN":"359"}]}""")),
            "https://sportsdb.test/"));
        return new FootballDataService(espnClient, sports, NullLogger<FootballDataService>.Instance);
    }

    public static ReportService Reports(FootballDataService football, NewsService news, WeatherService weather) =>
        new(football, news, weather, MsOptions.Create(Aggregator), NullLogger<ReportService>.Instance);
}
