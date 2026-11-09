using System.Globalization;
using Microsoft.Extensions.Options;
using UclAggregator.Clients.OpenMeteo;
using UclAggregator.Errors;
using UclAggregator.Models;
using UclAggregator.Options;

namespace UclAggregator.Services;

public sealed class WeatherService(
    OpenMeteoClient meteo,
    IOptions<OpenMeteoOptions> options,
    ILogger<WeatherService> logger)
{
    public async Task<Weather?> GetForMatchAsync(Match match, ApiResponse response, CancellationToken ct)
    {
        try
        {
            if (match.Date is null)
                return Skip(response, "the match has no kickoff time");

            var kickoff = match.Date.Value.UtcDateTime;
            var city = match.Venue?.City;
            if (string.IsNullOrWhiteSpace(city))
                return Skip(response, "the venue city is unknown");

            var coords = await GeocodeAsync(city, match.Venue?.Country, ct);
            if (coords is null)
                return Skip(response, $"'{city}' could not be geocoded");

            var archive = DateOnly.FromDateTime(kickoff) < DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-options.Value.ForecastPastDays));
            var hourly = (await meteo.GetHourlyAsync(coords.Value.Lat, coords.Value.Lon, DateOnly.FromDateTime(kickoff), archive, ct))?.Hourly;
            var times = hourly?.Time;
            var hour = times is null ? -1 : IndexOfHour(times, kickoff);
            if (hour < 0)
                return Skip(response, $"no hourly data exists for kickoff {kickoff:yyyy-MM-ddTHH:mm} UTC");

            var temperature = At(hourly!.Temperature, hour);
            var precipitation = At(hourly.Precipitation, hour);
            var wind = At(hourly.WindSpeed, hour);
            var weatherCode = At(hourly.WeatherCode, hour);
            if (temperature is null && precipitation is null && wind is null && weatherCode is null)
                return Skip(response, $"Open-Meteo has no values for kickoff hour {times![hour]}");

            return new Weather
            {
                MatchId = match.Id,
                Venue = match.Venue?.Name,
                Location = $"{city}, {match.Venue?.Country}",
                Time = times![hour],
                TemperatureC = temperature,
                PrecipitationMm = precipitation,
                WindKmh = wind,
                WeatherCode = weatherCode,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Weather provider failed");
            return Skip(response, "the weather provider failed");
        }
    }

    private static int IndexOfHour(List<string> times, DateTime kickoffUtc)
    {
        var prefix = kickoffUtc.ToString("yyyy-MM-ddTHH", CultureInfo.InvariantCulture);
        return times.FindIndex(t => t.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static Weather? Skip(ApiResponse response, string reason)
    {
        response.AddWarning(ErrorCode.DataNotAvailable, $"Weather unavailable: {reason}.");
        return null;
    }

    private static T? At<T>(List<T?>? list, int index) where T : struct =>
        list is not null && index < list.Count ? list[index] : null;

    private async Task<(double Lat, double Lon)?> GeocodeAsync(string city, string? country, CancellationToken ct)
    {
        var results = (await meteo.GeocodeAsync(city, ct))?.Results;
        var hit = results?.FirstOrDefault(r => string.Equals(r.Country, country, StringComparison.OrdinalIgnoreCase))
                  ?? results?.FirstOrDefault();
        return hit is null ? null : (hit.Latitude, hit.Longitude);
    }
}
