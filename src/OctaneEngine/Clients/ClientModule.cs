using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
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
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
#if NET6_0_OR_GREATER
            client.DefaultRequestVersion = System.Net.HttpVersion.Version20;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
#endif
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var config = sp.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            
#if !NETSTANDARD
            var socketsHandler = new SocketsHttpHandler
            {
                PreAuthenticate = true,
                Proxy = config.Proxy,
                UseProxy = config.UseProxy,
                MaxConnectionsPerServer = Math.Min(Math.Max(config.Parts * 4, 100), 1000),
                UseCookies = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                EnableMultipleHttp2Connections = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
#if NETCOREAPP3_0_OR_GREATER || NET5_0_OR_GREATER
                                         | DecompressionMethods.Brotli
#endif
                ,
                ConnectTimeout = TimeSpan.FromSeconds(15),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                KeepAlivePingDelay = TimeSpan.FromSeconds(20),
#if NET5_0_OR_GREATER
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
#endif
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsIOS())
            {
                int receiveBufferSize = Math.Max(1024 * 1024, config.BufferSize * 8); // 1MB minimum
                int sendBufferSize = 512 * 1024; // 512 KB
                byte[]? windowsKeepAliveSettings = null;

                if (OperatingSystem.IsWindows())
                {
                    windowsKeepAliveSettings = new byte[12];
                    BitConverter.GetBytes((uint)1).CopyTo(windowsKeepAliveSettings, 0); // Enable
                    BitConverter.GetBytes((uint)30000).CopyTo(windowsKeepAliveSettings, 4); // Time (ms)
                    BitConverter.GetBytes((uint)1000).CopyTo(windowsKeepAliveSettings, 8); // Interval (ms)
                }

                socketsHandler.ConnectCallback = async (context, cancellationToken) =>
                {
                    Socket? socket = null;

                    try
                    {
                        socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                        {
                            NoDelay = true,
                            ReceiveBufferSize = receiveBufferSize,
                            SendBufferSize = sendBufferSize,
                            LingerState = new LingerOption(false, 0)
                        };

                        try
                        {
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort, true);
                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        }
                        catch (SocketException) {}
                        catch (PlatformNotSupportedException) {}
                        
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                        
                        if (OperatingSystem.IsWindows() && windowsKeepAliveSettings != null)
                        {
                            socket.IOControl(IOControlCode.KeepAliveValues, windowsKeepAliveSettings, null);
                        }

                        await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        try
                        {
                            socket?.Dispose();
                        }
                        catch
                        {
                            // Ignore
                        }

                        throw;
                    }
                };
            }
            
            var primaryHandler = (HttpMessageHandler)socketsHandler;
#else
            var handler = new HttpClientHandler
            {
                PreAuthenticate = true,
                Proxy = config.Proxy,
                UseProxy = config.UseProxy,
                MaxConnectionsPerServer = Math.Min(Math.Max(config.Parts * 4, 100), 1000),
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };
            var primaryHandler = (HttpMessageHandler)handler;
#endif

            // Wrap with retry handler for resilience
            return new RetryHandler(primaryHandler, loggerFactory, config.NumRetries, config.RetryCap);
        });
    }
}