using UclAggregator.Metrics.Api;
using UclAggregator.Metrics.Clients;

namespace UclAggregator.Metrics;

public static class MetricsRegistration
{
    public static IServiceCollection AddAggregatorMetrics(this IServiceCollection services)
    {
        services.AddSingleton<SampleBuffer<ClientCallSample>>();
        services.AddSingleton<SampleBuffer<ApiCallSample>>();
        services.AddSingleton<StatsHistory<ClientStats>>();
        services.AddSingleton<StatsHistory<ApiStats>>();
        services.AddHostedService<MetricsJob>();
        return services;
    }
}
