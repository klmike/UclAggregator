using UclAggregator.Errors;
using UclAggregator.Models;
using UclAggregator.Services;
using UclAggregator.Tests.Support;
using Xunit;

namespace UclAggregator.Tests;

public class NewsServiceTests
{
    [Fact]
    public async Task Search_merges_sources_and_scores_by_order()
    {
        var google = Source("google", Article("g1", "One", "https://g.test/1"), Article("g2", "Two", "https://g.test/2"));
        var guardian = Source("guardian", Article("u1", "Three", "https://u.test/3"));
        var news = TestServices.News(google, guardian);

        var found = await news.SearchAsync("ucl", null, null, null, new ApiResponse(), CancellationToken.None);

        Assert.Equal(3, found.Count);
        Assert.Equal(["google", "google", "guardian"], found.Select(a => a.Source));
        Assert.Equal(1.0, found[0].Relevance);
        Assert.Equal(0.6667, found[1].Relevance);
        Assert.Equal(1.0, found[2].Relevance);
        Assert.Equal(found[0].Relevance, found[0].Score);
        Assert.Equal(new Uri("https://g.test/1"), found[0].Url);
    }

    [Fact]
    public async Task Search_skips_sources_that_do_not_apply()
    {
        var espn = new FakeNewsSource { Name = "espn", Applies = q => q.Team is not null, Articles = [Article("e1", "Team")] };
        var google = Source("google", Article("g1", "League"));
        var news = TestServices.News(espn, google);

        var found = await news.SearchAsync("ucl", null, null, null, new ApiResponse(), CancellationToken.None);

        Assert.Empty(espn.Queries);
        Assert.Single(found);
        Assert.Equal("google", found[0].Source);
    }

    [Fact]
    public async Task Search_keeps_successes_when_one_source_fails()
    {
        var down = new FakeNewsSource { Name = "guardian", Fail = new HttpRequestException("down") };
        var google = Source("google", Article("g1", "Ok", "https://g.test/1"));
        var response = new ApiResponse();

        var found = await TestServices.News(down, google)
            .SearchAsync("ucl", null, null, null, response, CancellationToken.None);

        Assert.Single(found);
        Assert.Equal("g1", found[0].Id);
        var warning = Assert.Single(response.Warnings);
        Assert.Equal(ErrorCode.ProviderUnavailable, warning.Code);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task Search_returns_empty_with_warnings_when_all_sources_fail()
    {
        var response = new ApiResponse();
        var found = await TestServices.News(
                new FakeNewsSource { Name = "google", Fail = new HttpRequestException("down") },
                new FakeNewsSource { Name = "guardian", Fail = new TimeoutException() })
            .SearchAsync("ucl", null, null, null, response, CancellationToken.None);

        Assert.Empty(found);
        Assert.Null(response.Error);
        Assert.Equal(2, response.Warnings.Count);
        Assert.Contains(response.Warnings, w => w.Code == ErrorCode.ProviderUnavailable);
        Assert.Contains(response.Warnings, w => w.Code == ErrorCode.ProviderTimeout);
    }

    [Fact]
    public async Task Search_drops_invalid_urls_and_halves_undated_score()
    {
        var source = Source("google",
            new SourceArticle { Id = "1", Title = "Now", Url = "not-a-url", PublishedAt = DateTimeOffset.UtcNow },
            new SourceArticle { Id = "2", Title = "Old", Url = "https://g.test/2" });

        var found = await TestServices.News(source).SearchAsync("ucl", null, null, null, new ApiResponse(), CancellationToken.None);

        Assert.Null(found[0].Url);
        Assert.Equal(new Uri("https://g.test/2"), found[1].Url);
        Assert.Equal(found[0].Relevance, found[0].Score);
        Assert.Equal(0.6667, found[1].Relevance);
        Assert.Equal(0.3333, found[1].Score);
    }

    [Fact]
    public async Task Search_skips_fallback_when_primary_has_enough_articles()
    {
        var guardian = Source("guardian", Article("u1", "One"), Article("u2", "Two"));
        var google = new FakeNewsSource { Name = "google", Fallback = true, Articles = [Article("g1", "Fallback")] };

        var found = await TestServices.News(guardian, google)
            .SearchAsync("ucl", null, null, null, new ApiResponse(), CancellationToken.None);

        Assert.Empty(google.Queries);
        Assert.Equal(["u1", "u2"], found.Select(a => a.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Search_uses_fallback_when_primary_has_too_few_articles(int primaryCount)
    {
        var guardian = Source("guardian", Enumerable.Range(0, primaryCount).Select(i => Article($"u{i}", $"T{i}")).ToArray());
        var google = new FakeNewsSource { Name = "google", Fallback = true, Articles = [Article("g1", "Fallback")] };

        var found = await TestServices.News(guardian, google)
            .SearchAsync("ucl", null, null, null, new ApiResponse(), CancellationToken.None);

        Assert.Single(google.Queries);
        Assert.Contains(found, a => a.Id == "g1");
        Assert.Equal(primaryCount + 1, found.Count);
    }

    [Fact]
    public async Task Search_uses_fallback_when_all_primaries_fail()
    {
        var guardian = new FakeNewsSource { Name = "guardian", Fail = new HttpRequestException("down") };
        var google = new FakeNewsSource { Name = "google", Fallback = true, Articles = [Article("g1", "Fallback")] };
        var response = new ApiResponse();

        var found = await TestServices.News(guardian, google)
            .SearchAsync("ucl", null, null, null, response, CancellationToken.None);

        Assert.Equal("g1", Assert.Single(found).Id);
        Assert.Equal(ErrorCode.ProviderUnavailable, Assert.Single(response.Warnings).Code);
    }

    [Fact]
    public async Task Search_with_no_sources_returns_empty()
    {
        var response = new ApiResponse();
        var found = await TestServices.News().SearchAsync("ucl", null, null, null, response, CancellationToken.None);
        Assert.Empty(found);
        Assert.Empty(response.Warnings);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task Search_forwards_dates_and_team()
    {
        var guardian = Source("guardian");
        var from = new DateOnly(2024, 1, 1);
        var to = new DateOnly(2024, 1, 31);
        await TestServices.News(guardian).SearchAsync("ucl", from, to, "Arsenal", new ApiResponse(), CancellationToken.None);
        var query = Assert.Single(guardian.Queries);
        Assert.Equal("ucl", query.Terms);
        Assert.Equal(from, query.From);
        Assert.Equal(to, query.To);
        Assert.Equal("Arsenal", query.Team);
    }

    [Fact]
    public void Combine_deduplicates_by_normalized_title_and_applies_limit()
    {
        var a = Item("A", "  The Title ", 0.4, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var b = Item("B", "the title", 0.9, new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var c = Item("C", "Other", 0.5, new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var d = Item("D", "Low", 0.1, new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero));

        var combined = NewsService.Combine([a, b, c, d], limit: 2);

        Assert.Equal(["B", "C"], combined.Select(x => x.Id));
    }

    [Fact]
    public void Combine_empty_or_null_titles_does_not_throw()
    {
        Assert.Empty(NewsService.Combine(Array.Empty<NewsArticle>(), 10));
        var kept = NewsService.Combine(
        [
            Item("a", null, 0.2, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Item("b", null, 0.9, new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)),
        ], 5);
        Assert.Equal("b", Assert.Single(kept).Id);
    }

    [Fact]
    public void Combine_breaks_score_ties_by_newer_date()
    {
        var older = Item("old", "Same", 0.8, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = Item("new", "Other", 0.8, new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var combined = NewsService.Combine([older, newer], 2);

        Assert.Equal(["new", "old"], combined.Select(x => x.Id));
    }

    private static FakeNewsSource Source(string name, params SourceArticle[] articles) =>
        new() { Name = name, Articles = articles };

    private static SourceArticle Article(string id, string title, string? url = null) =>
        new() { Id = id, Title = title, Url = url, PublishedAt = DateTimeOffset.UtcNow };

    private static NewsArticle Item(string id, string? title, double score, DateTimeOffset published) =>
        new() { Id = id, Source = "t", Title = title, Relevance = score, Score = score, PublishedAt = published };
}
