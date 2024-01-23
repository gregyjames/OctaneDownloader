using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngine;

namespace OctaneEngineCore;

public static class EngineBuilder
{
    public static IEngine Build(ILoggerFactory factory = null, IConfiguration config = null)
    {
        var containerBuilder = new ContainerBuilder();
        if (factory != null)
        {
            containerBuilder.RegisterInstance(factory).As<ILoggerFactory>();
        }

        if (config != null)
        {
            containerBuilder.RegisterInstance(config).As<IConfiguration>();
        }

        containerBuilder.AddOctane();
        var engineContainer = containerBuilder.Build();
        var engine = engineContainer.Resolve<IEngine>();
        return engine;
    }

    public static IEngine Build(ILoggerFactory factory = null, OctaneConfiguration config = null)
    {
        var containerBuilder = new ContainerBuilder();
        if (factory != null)
        {
            containerBuilder.RegisterInstance(factory).As<ILoggerFactory>();
        }

        if (config != null)
        {
            containerBuilder.RegisterInstance(config).As<IConfiguration>();
        }
        containerBuilder.AddOctane();
        var engineContainer = containerBuilder.Build();
        var engine = engineContainer.Resolve<IEngine>();
        return engine;
    }
}