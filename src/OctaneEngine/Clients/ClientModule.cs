using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OctaneEngineCore.Clients;

public static class ClientModule
{
    public static void AddClient(this IServiceCollection services)
    {
        services.AddSingleton<OctaneHttpClientPool>(provider =>
        {
            var config = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new OctaneHttpClientPool(config, loggerFactory);
        });
    }
}