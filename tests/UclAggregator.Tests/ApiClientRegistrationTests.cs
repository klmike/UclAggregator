using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UclAggregator.Clients;
using UclAggregator.Clients.Espn;
using UclAggregator.Clients.GoogleNews;
using UclAggregator.Clients.Guardian;
using UclAggregator.Clients.OpenMeteo;
using UclAggregator.Clients.TheSportsDb;
using UclAggregator.Metrics;
using UclAggregator.Options;
using UclAggregator.Services;
using Xunit;

namespace UclAggregator.Tests;

public class ApiClientRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddAggregatorMetrics();
        services.Configure<AggregatorOptions>(config.GetSection("Aggregator"));
        services.AddApiClients(config);
        services.AddScoped<FootballDataService>();
        services.AddScoped<NewsService>();
        services.AddScoped<WeatherService>();
        services.AddScoped<ReportService>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["Espn:BaseUrl"] = "https://espn.test/",
        ["Espn:StandingsUrl"] = "https://espn.test/standings",
        ["TheSportsDb:BaseUrl"] = "https://sportsdb.test/",
        ["OpenMeteo:BaseUrl"] = "https://meteo.test/",
        ["GoogleNews:BaseUrl"] = "https://news.test/",
        ["Guardian:BaseUrl"] = "https://guardian.test/",
        ["Guardian:ApiKey"] = "key",
    };

    [Fact]
    public void Resolves_every_client_and_binds_options()
    {
        using var sp = Build(ValidSettings());

        Assert.NotNull(sp.GetRequiredService<EspnClient>());
        Assert.NotNull(sp.GetRequiredService<TheSportsDbClient>());
        Assert.NotNull(sp.GetRequiredService<OpenMeteoClient>());
        Assert.NotNull(sp.GetRequiredService<GoogleNewsClient>());
        Assert.NotNull(sp.GetRequiredService<GuardianClient>());
        Assert.Equal("https://espn.test/standings", sp.GetRequiredService<IOptions<EspnOptions>>().Value.StandingsUrl);
        Assert.Equal("key", sp.GetRequiredService<IOptions<GuardianOptions>>().Value.ApiKey);
    }

    [Fact]
    public void Resolves_report_graph_from_a_scope()
    {
        using var sp = Build(ValidSettings());
        using var scope = sp.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ReportService>());
        var names = scope.ServiceProvider.GetServices<INewsSource>().Select(s => s.GetType().Name).Order().ToArray();
        Assert.Equal(["EspnNewsSource", "GoogleNewsClient", "GuardianClient"], names);
    }

    [Fact]
    public void Missing_base_url_fails_fast_when_client_is_resolved()
    {
        var settings = ValidSettings();
        settings.Remove("Guardian:BaseUrl");
        using var sp = Build(settings);

        var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<GuardianClient>());
        Assert.Contains("Guardian:BaseUrl", ex.Message);
    }
}
