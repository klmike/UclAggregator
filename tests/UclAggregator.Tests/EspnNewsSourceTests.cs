using UclAggregator.Clients;
using UclAggregator.Clients.Espn;
using UclAggregator.Options;
using UclAggregator.Tests.Support;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace UclAggregator.Tests;

public class EspnNewsSourceTests
{
    [Fact]
    public void Applies_only_when_query_names_a_team()
    {
        var source = new EspnNewsSource(
            new EspnClient(TestHttp.Throwing(new InvalidOperationException()), MsOptions.Create(new EspnOptions())),
            TestServices.Football(_ => TestHttp.Fail()));

        Assert.False(source.AppliesTo(new NewsQuery("ucl", null, null, null)));
        Assert.True(source.AppliesTo(new NewsQuery("ucl", null, null, "Arsenal")));
    }

    [Fact]
    public async Task Search_returns_empty_when_team_has_no_espn_id()
    {
        var espn = new EspnClient(TestHttp.Throwing(new InvalidOperationException("should not call")), MsOptions.Create(new EspnOptions()));
        var football = TestServices.Football(
            _ => TestHttp.Fail(),
            _ => TestHttp.Json("""{"teams":[{"strTeam":"Arsenal"}]}"""));
        var source = new EspnNewsSource(espn, football);

        var found = await source.SearchAsync(new NewsQuery("ucl", null, null, "Arsenal"), CancellationToken.None);
        Assert.Empty(found);
    }
}
