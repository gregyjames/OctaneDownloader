using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore.Clients;

namespace OctaneEngineCore;

public static class ContainerBuilderExtensions
{
    public static void AddOctane(this ContainerBuilder builder)
    {
        builder.RegisterModule(new ClientModule());
        builder.Register(ctx => new Engine(
            ctx.Resolve<OctaneConfiguration>(),
            ctx.ResolveKeyed<IClient>(ClientTypes.Octane),
            ctx.ResolveKeyed<IClient>(ClientTypes.Normal),
            ctx.Resolve<ILoggerFactory>())
        ).As<IEngine>().SingleInstance();
    }
}