using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore.Clients;

namespace OctaneEngineCore;

public class EngineModule: Module
{
    private readonly string _url;

    private async Task<(long, bool)> getFileSizeAndRangeSupport(string url)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        var responseLength = response.Content.Headers.ContentLength ?? 0;
        var rangeSupported = response.Headers.AcceptRanges.Contains("bytes");
        return (responseLength, rangeSupported);
    }
    public EngineModule(string url)
    {
        _url = url;
    }
    
    protected override void Load(ContainerBuilder builder)
    {
        var (responseLength, rangeSupported) = getFileSizeAndRangeSupport(_url).Result;
        builder.RegisterModule(new ClientModule(rangeSupported));
        builder.Register(ctx => new Engine(
            ctx.Resolve<ILoggerFactory>(), 
            ctx.Resolve<OctaneConfiguration>(),
            ctx.Resolve<IClient>(), 
            responseLength, 
            rangeSupported)
        ).As<IEngine>().SingleInstance();
    }
}