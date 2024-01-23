using System.IO;
using System.Threading;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester.Examples;

public class NoLogger
{
    public void NoLoggerExample()
    {
        #region Configuration Loading
        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        var configRoot = builder.Build();
        #endregion
        
        var pauseTokenSource = new PauseTokenSource();
        using var cancelTokenSource = new CancellationTokenSource();
        var containerBuilder = new ContainerBuilder();
        containerBuilder.RegisterInstance(configRoot).As<IConfiguration>();
        containerBuilder.AddOctane();
        var engineContainer = containerBuilder.Build();
        var engine = engineContainer.Resolve<IEngine>();
        engine.DownloadFile("", "", pauseTokenSource, cancelTokenSource);
    }
}