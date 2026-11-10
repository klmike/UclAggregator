namespace UclAggregator.Clients.OpenMeteo;

public sealed class OpenMeteoOptions
{
    public string BaseUrl { get; set; } = "";
    public string GeocodingUrl { get; set; } = "";
    public string ForecastUrl { get; set; } = "";
    public string ArchiveUrl { get; set; } = "";
    public string Hourly { get; set; } = "";
    public int GeocodeCount { get; set; } = 10;
    public int ForecastPastDays { get; set; } = 90;
}
