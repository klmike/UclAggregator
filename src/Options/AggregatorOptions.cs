namespace UclAggregator.Options;

public sealed class AggregatorOptions
{
    public string DefaultKeywords { get; set; } = "Champions League";
    public int TopPlayersWithNews { get; set; } = 3;
    public int DefaultNewsLimit { get; set; } = 20;
    public double NewsHalfLifeDays { get; set; } = 7;
    public int NewsFallbackMaxArticles { get; set; } = 1;
    public double CacheHours { get; set; } = 12;
}
