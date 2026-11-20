using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OctaneEngineCore.Clients;

public static class ClientModule
{
    public static void AddClient(this IServiceCollection services)
    {
        services.AddHttpClient("OctaneClient", (sp, client) =>
        {
            var config = sp.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            client.Timeout = TimeSpan.FromMinutes(30); // Reasonable timeout for large downloads
            client.DefaultRequestHeaders.Add("User-Agent", "OctaneEngine/1.0");
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestVersion = config.HttpVersion ?? System.Net.HttpVersion.Version11;
            client.DefaultVersionPolicy = config.HttpVersionPolicy;
        })
        .RemoveAllLoggers()
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var config = sp.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            
            var socketsHandler = new SocketsHttpHandler
            {
                PreAuthenticate = true,
                Proxy = config.Proxy,
                UseProxy = config.UseProxy,
                MaxConnectionsPerServer = Math.Clamp(config.Parts * 4, 100, 1000),
                UseCookies = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                EnableMultipleHttp2Connections = true,
                InitialHttp2StreamWindowSize = 16 * 1024 * 1024, // 16 MB for high-BDP links
                AutomaticDecompression = DecompressionMethods.None,
                ConnectTimeout = TimeSpan.FromSeconds(15),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                KeepAlivePingDelay = TimeSpan.FromSeconds(20),
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            // Rely on SocketsHttpHandler's built-in Happy Eyeballs (RFC 8305) connect logic
            // across macOS, Linux, and Windows instead of a custom ConnectCallback that
            // serializes DNS addresses and disables IPv4/IPv6 racing.
            return new RetryHandler(socketsHandler, loggerFactory, config.NumRetries, config.RetryCap);
        });
    }
}