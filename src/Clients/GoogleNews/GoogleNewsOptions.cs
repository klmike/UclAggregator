namespace UclAggregator.Clients.GoogleNews;

public sealed class GoogleNewsOptions
{
    public string BaseUrl { get; set; } = "";
    public string Language { get; set; } = "en-GB";
    public string Region { get; set; } = "GB";
    public string Edition { get; set; } = "GB:en";
    public int MaxItems { get; set; } = 20;
}
