using System.Net.Http;
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
            
            var clientHandler = new HttpClientHandler()
            {
                PreAuthenticate = true,
                UseDefaultCredentials = true,
                Proxy = cfg.Proxy,
                UseProxy = cfg.UseProxy,
                MaxConnectionsPerServer = cfg.Parts,
                UseCookies = false,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            var retryHandler = new RetryHandler(clientHandler, factory, cfg.NumRetries);
            //var basePart = new Uri(new Uri(_url).GetLeftPart(UriPartial.Authority));
            var _client = new HttpClient(retryHandler)
            {
                MaxResponseContentBufferSize = cfg.BufferSize,
                //BaseAddress = basePart
            };

            return _client;
        }).As<HttpClient>().SingleInstance();
    }
}