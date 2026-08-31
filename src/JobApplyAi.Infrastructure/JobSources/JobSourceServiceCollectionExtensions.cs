using JobApplyAi.Domain.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplyAi.Infrastructure.JobSources;

public static class JobSourceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the three source adapters as typed HttpClients, each with the standard
    /// resilience pipeline (retry w/ jittered backoff on 5xx/429/timeouts, circuit breaker) so a
    /// slow or failing source can't starve the others.
    /// </summary>
    public static IServiceCollection AddJobSourceClients(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AdzunaOptions>(configuration.GetSection(AdzunaOptions.SectionName));

        services.AddHttpClient<GreenhouseJobSourceClient>(c =>
                c.BaseAddress = new Uri(GreenhouseJobSourceClient.BaseUrl))
            .AddStandardResilienceHandler();
        services.AddHttpClient<LeverJobSourceClient>(c =>
                c.BaseAddress = new Uri(LeverJobSourceClient.BaseUrl))
            .AddStandardResilienceHandler();
        services.AddHttpClient<AdzunaJobSourceClient>(c =>
                c.BaseAddress = new Uri(AdzunaJobSourceClient.BaseUrl))
            .AddStandardResilienceHandler();

        services.AddTransient<IJobSourceClient>(sp => sp.GetRequiredService<GreenhouseJobSourceClient>());
        services.AddTransient<IJobSourceClient>(sp => sp.GetRequiredService<LeverJobSourceClient>());
        services.AddTransient<IJobSourceClient>(sp => sp.GetRequiredService<AdzunaJobSourceClient>());

        return services;
    }
}
