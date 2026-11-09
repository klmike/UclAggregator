using Microsoft.Extensions.DependencyInjection.Extensions;
using UclAggregator.Metrics;
using UclAggregator.Metrics.Clients;

namespace UclAggregator.Clients;

public static class ApiClientRegistration
{
    public static IServiceCollection AddApiClient<TClient>(this IServiceCollection services, IConfiguration config, string section)
        where TClient : class
    {
        services.TryAddTransient<CachingHandler>();
        var client = services.AddHttpClient<TClient>(c =>
        {
            c.BaseAddress = new Uri(config[$"{section}:BaseUrl"]
                ?? throw new InvalidOperationException($"Missing configuration: {section}:BaseUrl"));
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(config["Http:UserAgent"] ?? "UclAggregator/1.0");
        });
        client.AddHttpMessageHandler(sp => new ClientMetricsHandler(
            typeof(TClient).Name, sp.GetRequiredService<SampleBuffer<ClientCallSample>>()));
        client.AddHttpMessageHandler<CachingHandler>();
        return services;
    }
}
