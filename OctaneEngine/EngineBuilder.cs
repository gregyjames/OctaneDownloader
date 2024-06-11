using System.Collections.Generic;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
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
            var builder = new ConfigurationBuilder();
            var configuration = builder.AddInMemoryCollection(new Dictionary<string, string> {
                ["octane:Parts"] = config.Parts.ToString(),
                ["octane:BufferSize"] = config.BufferSize.ToString(),
                ["octane:ShowProgress"] = config.ShowProgress.ToString(),
                ["octane:NumRetries"] = config.NumRetries.ToString(),
                ["octane:BytesPerSecond"] = config.BytesPerSecond.ToString(),
                ["octane:UseProxy"] = config.UseProxy.ToString(),
                ["octane:LowMemoryMode"] = config.LowMemoryMode.ToString(),
            }).Build();
            containerBuilder.RegisterInstance(configuration).As<IConfiguration>();
        }
        containerBuilder.AddOctane();
        var engineContainer = containerBuilder.Build();
        var engine = engineContainer.Resolve<IEngine>();
        return engine;
    }
}