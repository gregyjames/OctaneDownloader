using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore.ColorConsoleLogger;
using OctaneEngineCore.ShellProgressBar;
using OctaneEngineCore.Streams;

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
        
        // Register Client
        builder.RegisterType<OctaneClient>().As<IClient>().SingleInstance().Keyed<IClient>(ClientTypes.Octane);
        builder.RegisterType<DefaultClient>().As<IClient>().SingleInstance().Keyed<IClient>(ClientTypes.Normal);

    }
}