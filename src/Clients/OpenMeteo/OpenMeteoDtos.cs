using System.Text.Json.Serialization;

namespace UclAggregator.Clients.OpenMeteo;

public sealed record OpenMeteoGeocode(List<OpenMeteoPlace>? Results, bool? Error, string? Reason);

public sealed record OpenMeteoPlace(string? Country, double Latitude, double Longitude);

public sealed record OpenMeteoForecast(OpenMeteoHourly? Hourly, bool? Error, string? Reason);

public sealed record OpenMeteoHourly(
    List<string>? Time,
    [property: JsonPropertyName("temperature_2m")] List<double?>? Temperature,
    List<double?>? Precipitation,
    [property: JsonPropertyName("wind_speed_10m")] List<double?>? WindSpeed,
    [property: JsonPropertyName("weather_code")] List<int?>? WeatherCode);
