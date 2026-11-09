namespace UclAggregator.Models;

public class NewsArticle
{
    public required string Id { get; set; }
    public required string Source { get; set; }
    public string? Publisher { get; set; }
    public string? Title { get; set; }
    public Uri? Url { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public required double Relevance { get; set; }
    public required double Score { get; set; }
}

public class SourceArticle
{
    public required string Id { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? Publisher { get; set; }
}
