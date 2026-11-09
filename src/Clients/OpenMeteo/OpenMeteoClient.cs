using System.Globalization;
using Microsoft.Extensions.Options;
using UclAggregator.Errors;
using UclAggregator.Options;

namespace UclAggregator.Clients.OpenMeteo;

public sealed class OpenMeteoClient(HttpClient http, IOptions<OpenMeteoOptions> options)
{
    private readonly OpenMeteoOptions _opt = options.Value;

    public async Task<OpenMeteoGeocode?> GeocodeAsync(string city, CancellationToken ct)
    {
        var body = await http.GetFromJsonAsync<OpenMeteoGeocode>(
            $"{_opt.GeocodingUrl}?name={Uri.EscapeDataString(city)}&count={_opt.GeocodeCount}", ApiJson.Options, ct);
        return Check(body, body?.Error, body?.Reason);
    }

    public async Task<OpenMeteoForecast?> GetHourlyAsync(double lat, double lon, DateOnly date, bool archive, CancellationToken ct)
    {
        var inv = CultureInfo.InvariantCulture;
        var url = $"{(archive ? _opt.ArchiveUrl : _opt.ForecastUrl)}?latitude={lat.ToString(inv)}&longitude={lon.ToString(inv)}" +
                  $"&hourly={_opt.Hourly}&start_date={date:yyyy-MM-dd}&end_date={date:yyyy-MM-dd}&timezone=UTC";
        var body = await http.GetFromJsonAsync<OpenMeteoForecast>(url, ApiJson.Options, ct);
        return Check(body, body?.Error, body?.Reason);
    }

    // Open-Meteo reports some failures inside a 200 response.
    private static T? Check<T>(T? body, bool? error, string? reason) where T : class =>
        error == true ? throw new AppException(ErrorCode.ProviderBadResponse, $"Open-Meteo reported an error: {reason}") : body;
}
