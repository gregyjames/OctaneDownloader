using System;
using System.Net.Http;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore.ColorConsoleLogger;
using OctaneEngineCore.ShellProgressBar;
using OctaneEngineCore.Streams;

namespace OctaneEngineCore.Clients;

public class ClientModule: Module
{
    private readonly bool _rangeSupported;

    public ClientModule(bool rangeSupported)
    {
        _rangeSupported = rangeSupported;
    }

    protected override void Load(ContainerBuilder builder)
    {
        // Logging 
        builder.RegisterModule(new LoggerModule());

        // Config
        builder.Register(context =>
        {
            var registered = context.TryResolve(out IConfiguration config);
            var OCconfig = !registered ? new OctaneConfiguration() : new OctaneConfiguration(config, context.Resolve<ILoggerFactory>());

            if (OCconfig.ShowProgress)
            {
                // Register ProgressBar
                builder.RegisterModule(new ProgressModule());
            }
            return OCconfig;
        }).As<OctaneConfiguration>().IfNotRegistered(typeof(OctaneConfiguration)).SingleInstance();
        
        // Register HTTPClient Instance
        builder.RegisterModule(new HTTPClientModule());
        
        // Register stream type
        builder.RegisterModule(new StreamModule());

        // Register Client
        if (_rangeSupported)
        {
            builder.RegisterType<OctaneClient>().As<IClient>().SingleInstance();
        }
        else
        {
            builder.RegisterType<DefaultClient>().As<IClient>().SingleInstance();
        }

    }
}