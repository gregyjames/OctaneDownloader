using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OctaneEngine;

namespace OctaneEngineCore.Clients;

public static class HTTPClientModule
{
    internal static void AddHTTPClient(this IServiceCollection services)
    {
        services.AddTransient<SocketsHttpHandler>(provider =>
        {
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            return new SocketsHttpHandler()
            {
                PreAuthenticate = true,
                Proxy = cfg.Proxy,
                UseProxy = cfg.UseProxy,
                MaxConnectionsPerServer = cfg.Parts * 2,
                UseCookies = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                EnableMultipleHttp2Connections = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ConnectTimeout = TimeSpan.FromSeconds(15), // faster failure on bad endpoints
            };
        });

        services.AddTransient<RetryHandler>(provider =>
        {
            var handler = provider.GetRequiredService<SocketsHttpHandler>();
            var factory = provider.GetRequiredService<ILoggerFactory>();
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            
            return new RetryHandler(handler, factory, cfg.NumRetries, cfg.RetryCap);
        });

        services.AddTransient<HttpClient>(provider =>
        {
            var handler = provider.GetRequiredService<RetryHandler>();
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            return new HttpClient(handler)
            {
                MaxResponseContentBufferSize = cfg.BufferSize,
            };
        });
    }
}