using System.Xml.Linq;
using Microsoft.Extensions.Options;
using UclAggregator.Models;
using UclAggregator.Options;

namespace UclAggregator.Clients.GoogleNews;

public sealed class GoogleNewsClient(HttpClient http, IOptions<GoogleNewsOptions> options) : INewsSource
{
    private readonly GoogleNewsOptions _opt = options.Value;

    public string Name => "google";

    public bool AppliesTo(NewsQuery query) => true;

    public bool Fallback => true;

    public async Task<IReadOnlyList<SourceArticle>> SearchAsync(NewsQuery query, CancellationToken ct)
    {
        var q = query.Terms;
        if (query.From is { } a) q += $" after:{a:yyyy-MM-dd}";
        if (query.To is { } b) q += $" before:{b.AddDays(1):yyyy-MM-dd}";

        await using var stream = await http.GetStreamAsync(
            $"search?q={Uri.EscapeDataString(q)}&hl={_opt.Language}&gl={_opt.Region}&ceid={Uri.EscapeDataString(_opt.Edition)}", ct);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
        return doc.Descendants("item").Take(_opt.MaxItems)
            .Select(i => Map(
                (string?)i.Element("guid"),
                (string?)i.Element("title"),
                (string?)i.Element("link"),
                (string?)i.Element("pubDate"),
                (string?)i.Element("source")))
            .ToList();
    }

    private static SourceArticle Map(string? guid, string? fullTitle, string? link, string? pubDate, string? source)
    {
        var full = fullTitle ?? "";
        var cut = full.LastIndexOf(" - ", StringComparison.Ordinal);
        var title = cut > 0 ? full[..cut] : full;
        var publisher = source ?? (cut > 0 ? full[(cut + 3)..] : null);
        return new SourceArticle
        {
            Id = $"google:{guid}",
            Title = title,
            Url = link,
            PublishedAt = DateParsing.Parse(pubDate),
            Publisher = publisher,
        };
    }
}
