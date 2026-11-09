using Microsoft.Extensions.Options;
using UclAggregator.Models;
using UclAggregator.Options;

namespace UclAggregator.Clients.Guardian;

public sealed class GuardianClient(HttpClient http, IOptions<GuardianOptions> options) : INewsSource
{
    private readonly GuardianOptions _opt = options.Value;

    public string Name => "guardian";

    public bool AppliesTo(NewsQuery query) => !string.IsNullOrWhiteSpace(_opt.ApiKey);

    public async Task<IReadOnlyList<SourceArticle>> SearchAsync(NewsQuery query, CancellationToken ct)
    {
        var url = $"search?q={Uri.EscapeDataString(query.Terms)}&section={_opt.Section}&order-by={_opt.OrderBy}&page-size={_opt.PageSize}&api-key={_opt.ApiKey}";
        if (query.From is { } f) url += $"&from-date={f:yyyy-MM-dd}";
        if (query.To is { } t) url += $"&to-date={t:yyyy-MM-dd}";

        var body = (await http.GetFromJsonAsync<GuardianResponse>(url, ApiJson.Options, ct))?.Response;
        if (body is not { Status: "ok" })
            throw new HttpRequestException($"Guardian error: {body?.Message}");

        return (body.Results ?? []).Select(r =>
            new SourceArticle
            {
                Id = $"guardian:{r.Id}",
                Title = r.WebTitle,
                Url = r.WebUrl,
                PublishedAt = DateParsing.Parse(r.WebPublicationDate),
                Publisher = "The Guardian",
            }).ToList();
    }
}
