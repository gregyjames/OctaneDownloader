using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctaneEngine;
using OctaneEngineCore.ShellProgressBar;

namespace OctaneEngineCore.Clients;

public enum ClientTypes { Octane, Normal }
public static class ClientModule
{
    public static void AddClient(this IServiceCollection services)
    {
        services.AddHTTPClient();

        services.AddKeyedTransient<IClient>(ClientTypes.Octane, (provider, o) =>
        {
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            var client = provider.GetRequiredService<HttpClient>();
            var factory = provider.GetRequiredService<ILoggerFactory>();
            var progress = provider.GetService<ProgressBar>();
            
            return new OctaneClient(cfg, client, factory, progress);
        });
        services.AddKeyedSingleton<IClient>(ClientTypes.Normal, (provider, o) =>
        {
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            var client = provider.GetRequiredService<HttpClient>();
            var progress = provider.GetService<ProgressBar>();

            return new DefaultClient(client, cfg, progress);
        });
    }
}