using System;
using System.Net.Http;
using System.Net.Security;
using Autofac;
using Microsoft.Extensions.Logging;
using OctaneEngine;

namespace OctaneEngineCore.Clients;

public class HTTPClientModule: Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(ctx =>
        {
            var factory = ctx.Resolve<ILoggerFactory>();
            var cfg = ctx.Resolve<OctaneConfiguration>();
            
            var socketsHandler = new SocketsHttpHandler
            {
                PreAuthenticate = true,
                Proxy = cfg.Proxy,
                UseProxy = cfg.UseProxy,
                MaxConnectionsPerServer = cfg.Parts,
                UseCookies = false,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                },
                PooledConnectionLifetime = TimeSpan.FromMinutes(10), // Recycle connections every 10 minutes,
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                EnableMultipleHttp2Connections = true
            };

            var retryHandler = new RetryHandler(socketsHandler, factory, cfg.NumRetries);
            var _client = new HttpClient(retryHandler)
            {
                MaxResponseContentBufferSize = cfg.BufferSize
            };

            return _client;
        }).As<HttpClient>().SingleInstance();
    }
}