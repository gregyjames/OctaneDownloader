using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngine;

namespace OctaneEngineCore;

public static class EngineBuilder
{
    public static IEngine Build(ILoggerFactory factory, IConfiguration config)
    {
        var containerBuilder = new ContainerBuilder();
        containerBuilder.RegisterInstance(factory).As<ILoggerFactory>();
        containerBuilder.RegisterInstance(config).As<IConfiguration>();
        containerBuilder.AddOctane();
        var engineContainer = containerBuilder.Build();
        var engine = engineContainer.Resolve<IEngine>();
        return engine;
    }

    public static IEngine Build(ILoggerFactory factory, OctaneConfiguration config)
    {
        var containerBuilder = new ContainerBuilder();
        containerBuilder.RegisterInstance(factory).As<ILoggerFactory>();
        containerBuilder.RegisterInstance(config).As<OctaneConfiguration>();
        containerBuilder.AddOctane();
        var engineContainer = containerBuilder.Build();
        var engine = engineContainer.Resolve<IEngine>();
        return engine;
    }
}