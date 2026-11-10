using Microsoft.Extensions.DependencyInjection.Extensions;
using UclAggregator.Clients.Espn;
using UclAggregator.Clients.GoogleNews;
using UclAggregator.Clients.Guardian;
using UclAggregator.Clients.OpenMeteo;
using UclAggregator.Clients.TheSportsDb;
using UclAggregator.Metrics;
using UclAggregator.Metrics.Clients;

namespace UclAggregator.Clients;

public static class ApiClientRegistration
{
    public static IServiceCollection AddApiClients(this IServiceCollection services, IConfiguration config)
    {
        services.AddApiClient<EspnClient, EspnOptions>(config, "Espn");
        services.AddApiClient<TheSportsDbClient>(config, "TheSportsDb");
        services.AddApiClient<OpenMeteoClient, OpenMeteoOptions>(config, "OpenMeteo");
        services.AddNewsClient<GoogleNewsClient, GoogleNewsOptions>(config, "GoogleNews");
        services.AddNewsClient<GuardianClient, GuardianOptions>(config, "Guardian");
        services.AddScoped<INewsSource, EspnNewsSource>();
        return services;
    }

    private static void AddNewsClient<TClient, TOptions>(this IServiceCollection services, IConfiguration config, string section)
        where TClient : class, INewsSource
        where TOptions : class
    {
        services.AddApiClient<TClient, TOptions>(config, section);
        services.AddScoped<INewsSource>(sp => sp.GetRequiredService<TClient>());
    }

    private static void AddApiClient<TClient, TOptions>(this IServiceCollection services, IConfiguration config, string section)
        where TClient : class
        where TOptions : class
    {
        services.Configure<TOptions>(config.GetSection(section));
        services.AddApiClient<TClient>(config, section);
    }

    private static IServiceCollection AddApiClient<TClient>(this IServiceCollection services, IConfiguration config, string section)
        where TClient : class
    {
        services.TryAddTransient<CachingHandler>();
        var client = services.AddHttpClient<TClient>(c =>
        {
            c.BaseAddress = new Uri(config[$"{section}:BaseUrl"]
                ?? throw new InvalidOperationException($"Missing configuration: {section}:BaseUrl"));
            c.Timeout = TimeSpan.FromSeconds(10); // a slow provider should not hold up the whole aggregated response
            c.DefaultRequestHeaders.UserAgent.ParseAdd(config["Http:UserAgent"] ?? "UclAggregator/1.0");
        });
        // metrics goes first (outermost) so it also sees the calls the cache answers
        client.AddHttpMessageHandler(sp => new ClientMetricsHandler(
            typeof(TClient).Name, sp.GetRequiredService<SampleBuffer<ClientCallSample>>()));
        client.AddHttpMessageHandler<CachingHandler>();
        return services;
    }
}
