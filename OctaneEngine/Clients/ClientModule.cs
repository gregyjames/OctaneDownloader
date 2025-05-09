using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore.ColorConsoleLogger;
using OctaneEngineCore.ShellProgressBar;

namespace OctaneEngineCore.Clients;

public enum ClientTypes { Octane, Normal }
public class ClientModule: Module
{
    public ClientModule()
    {
    }

    protected override void Load(ContainerBuilder builder)
    {
        // Logging 
        builder.RegisterModule(new LoggerModule());

        // Config
        builder.Register(context =>
        {
            var registered = context.TryResolve(out IConfiguration config);
            var octaneConfiguration = !registered ? new OctaneConfiguration() : new OctaneConfiguration(config, context.Resolve<ILoggerFactory>());

            if (octaneConfiguration.ShowProgress)
            {
                // Register ProgressBar
                builder.RegisterModule(new ProgressModule());
            }
            return octaneConfiguration;
        }).As<OctaneConfiguration>().IfNotRegistered(typeof(OctaneConfiguration)).SingleInstance();
        
        // Register HTTPClient Instance
        builder.RegisterModule(new HTTPClientModule());
        
        // Register Client
        builder.RegisterType<OctaneClient>().As<IClient>().SingleInstance().Keyed<IClient>(ClientTypes.Octane);
        builder.RegisterType<DefaultClient>().As<IClient>().SingleInstance().Keyed<IClient>(ClientTypes.Normal);

    }
}