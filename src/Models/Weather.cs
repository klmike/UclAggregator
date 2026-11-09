namespace UclAggregator.Models;

public class Weather
{
    public string? MatchId { get; set; }
    public string? Venue { get; set; }
    public required string Location { get; set; }
    public string? Time { get; set; }
    public double? TemperatureC { get; set; }
    public double? PrecipitationMm { get; set; }
    public double? WindKmh { get; set; }
    public int? WeatherCode { get; set; }
}
