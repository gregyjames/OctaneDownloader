using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctaneEngine;

namespace OctaneEngineCore.Clients;

public static class HTTPClientModule
{
    internal static IServiceCollection AddHTTPClient(this IServiceCollection services)
    {
        services.AddSingleton<HttpClientHandler>(provider =>
        {
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            return new HttpClientHandler()
            {
                PreAuthenticate = true,
                UseDefaultCredentials = true,
                Proxy = cfg.Proxy,
                UseProxy = cfg.UseProxy,
                MaxConnectionsPerServer = cfg.Parts,
                UseCookies = false,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
        });

        services.AddSingleton<RetryHandler>(provider =>
        {
            var handler = provider.GetRequiredService<HttpClientHandler>();
            var factory = provider.GetRequiredService<ILoggerFactory>();
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            
            return new RetryHandler(handler, factory, cfg.NumRetries);
        });

        services.AddSingleton<HttpClient>(provider =>
        {
            var handler = provider.GetRequiredService<RetryHandler>();
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            return new HttpClient(handler)
            {
                MaxResponseContentBufferSize = cfg.BufferSize,
            };
        });

        return services;
    }
}