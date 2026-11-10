namespace UclAggregator.Clients.Guardian;

public sealed class GuardianOptions
{
    public string BaseUrl { get; set; } = "";
    public string? ApiKey { get; set; }
    public string Section { get; set; } = "football";
    public string OrderBy { get; set; } = "relevance";
    public int PageSize { get; set; } = 20;
}
