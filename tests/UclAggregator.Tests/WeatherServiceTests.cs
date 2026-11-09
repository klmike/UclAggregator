using System.Globalization;
using UclAggregator.Errors;
using UclAggregator.Models;
using UclAggregator.Tests.Support;
using Xunit;

namespace UclAggregator.Tests;

public class WeatherServiceTests
{
    [Fact]
    public async Task Skip_when_kickoff_or_city_missing()
    {
        var weather = TestServices.Weather(TestHttp.Throwing(new InvalidOperationException("should not call")));
        var noDate = new ApiResponse();
        var noCity = new ApiResponse();

        Assert.Null(await weather.GetForMatchAsync(Match(null, "London"), noDate, CancellationToken.None));
        Assert.Null(await weather.GetForMatchAsync(Match(Kickoff, null), noCity, CancellationToken.None));
        Assert.Equal(ErrorCode.DataNotAvailable, Assert.Single(noDate.Warnings).Code);
        Assert.Equal(ErrorCode.DataNotAvailable, Assert.Single(noCity.Warnings).Code);
    }

    [Fact]
    public async Task Skip_when_geocode_misses()
    {
        var weather = TestServices.Weather(TestHttp.Client(_ => TestHttp.Json("""{"results":[]}""")));
        var response = new ApiResponse();

        var result = await weather.GetForMatchAsync(Match(Kickoff, "London"), response, CancellationToken.None);

        Assert.Null(result);
        Assert.Contains("could not be geocoded", Assert.Single(response.Warnings).Message);
    }

    [Fact]
    public async Task Prefers_geocode_result_matching_country()
    {
        var weather = TestServices.Weather(TestHttp.Client(request =>
        {
            if (TestHttp.Has(request, "geocode"))
                return TestHttp.Json("""
                    {"results":[
                      {"country":"France","latitude":48.8,"longitude":2.3},
                      {"country":"England","latitude":51.5,"longitude":-0.1}
                    ]}
                    """);
            Assert.Contains("latitude=51.5", request.RequestUri!.Query);
            return Hourly(Kickoff, 12.3, 0.1, 8, 1);
        }));

        var result = await weather.GetForMatchAsync(Match(Kickoff, "London", "England"), new ApiResponse(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(12.3, result.TemperatureC);
        Assert.Equal(0.1, result.PrecipitationMm);
        Assert.Equal(8, result.WindKmh);
        Assert.Equal(1, result.WeatherCode);
        Assert.Equal("London, England", result.Location);
    }

    [Fact]
    public async Task Skip_when_hour_or_values_missing()
    {
        var noHour = TestServices.Weather(TestHttp.Client(request =>
            TestHttp.Has(request, "geocode")
                ? Geocode()
                : TestHttp.Json("""{"hourly":{"time":["1999-01-01T00:00"],"temperature_2m":[1],"precipitation":[0],"wind_speed_10m":[0],"weather_code":[0]}}""")));
        var empty = TestServices.Weather(TestHttp.Client(request =>
            TestHttp.Has(request, "geocode")
                ? Geocode()
                : Hourly(Kickoff, null, null, null, null)));

        var missingHour = new ApiResponse();
        var missingValues = new ApiResponse();
        Assert.Null(await noHour.GetForMatchAsync(Match(Kickoff, "London"), missingHour, CancellationToken.None));
        Assert.Null(await empty.GetForMatchAsync(Match(Kickoff, "London"), missingValues, CancellationToken.None));
        Assert.Contains("no hourly data", Assert.Single(missingHour.Warnings).Message);
        Assert.Contains("no values", Assert.Single(missingValues.Warnings).Message);
    }

    [Fact]
    public async Task Skip_when_provider_returns_error_payload()
    {
        var weather = TestServices.Weather(TestHttp.Client(_ => TestHttp.Json("""{"error":true,"reason":"limit"}""")));
        var response = new ApiResponse();

        Assert.Null(await weather.GetForMatchAsync(Match(Kickoff, "London"), response, CancellationToken.None));
        Assert.Contains("weather provider failed", Assert.Single(response.Warnings).Message);
    }

    [Fact]
    public async Task Skip_when_provider_throws()
    {
        var weather = TestServices.Weather(TestHttp.Throwing(new HttpRequestException("down")));
        var response = new ApiResponse();

        Assert.Null(await weather.GetForMatchAsync(Match(Kickoff, "London"), response, CancellationToken.None));
        Assert.Contains("weather provider failed", Assert.Single(response.Warnings).Message);
    }

    [Fact]
    public async Task Uses_archive_url_for_old_kickoff()
    {
        var kickoff = DateTimeOffset.UtcNow.AddDays(-100);
        Uri? forecast = null;
        var weather = TestServices.Weather(TestHttp.Client(request =>
        {
            if (TestHttp.Has(request, "geocode")) return Geocode();
            forecast = request.RequestUri;
            return Hourly(kickoff, 5, 0, 1, 0);
        }));

        var result = await weather.GetForMatchAsync(Match(kickoff, "London"), new ApiResponse(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("archive.test", forecast!.Host);
    }

    private static readonly DateTimeOffset Kickoff = new(2024, 5, 1, 19, 30, 0, TimeSpan.Zero);

    private static Match Match(DateTimeOffset? date, string? city, string? country = "England") =>
        new() { Source = "espn", Date = date, Venue = city is null ? null : new Venue { Name = "Wembley", City = city, Country = country } };

    private static HttpResponseMessage Geocode() =>
        TestHttp.Json("""{"results":[{"country":"England","latitude":51.5,"longitude":-0.1}]}""");

    private static HttpResponseMessage Hourly(DateTimeOffset kickoff, double? temp, double? rain, double? wind, int? code)
    {
        var time = kickoff.UtcDateTime.ToString("yyyy-MM-ddTHH:00", CultureInfo.InvariantCulture);
        return TestHttp.Json(
            "{\"hourly\":{\"time\":[\"" + time + "\"],\"temperature_2m\":[" + N(temp) +
            "],\"precipitation\":[" + N(rain) + "],\"wind_speed_10m\":[" + N(wind) +
            "],\"weather_code\":[" + (code?.ToString() ?? "null") + "]}}");
    }

    private static string N(double? value) => value is null ? "null" : value.Value.ToString(CultureInfo.InvariantCulture);
}
