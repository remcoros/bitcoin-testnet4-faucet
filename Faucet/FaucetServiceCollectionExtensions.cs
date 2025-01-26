using Faucet.Data;
using Faucet.Wallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Faucet;

public static class FaucetServiceCollectionExtensions
{
    public static IServiceCollection AddFaucetServices(this IServiceCollection services, Action<FaucetOptions> configureOptions)
    {
        services.AddLogging();
        services.AddMemoryCache();
        services.AddOptions<FaucetOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddTransient<FaucetOptions>(x => x.GetRequiredService<IOptions<FaucetOptions>>().Value);
        services.AddDbContext<FaucetDbContext>();
        services.AddSingleton<FaucetWallet>();
        services.AddHttpClient(nameof(FaucetWallet))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                // Ignore SSL certificate errors (for our self-signed cert)
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        services.AddScoped<FaucetServices>();

        return services;
    }
}