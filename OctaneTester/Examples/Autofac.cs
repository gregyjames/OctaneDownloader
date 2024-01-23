using System.IO;
using System.Threading;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester.Examples;

public class Autofac
{
    public void AutofacExample()
    {
        var seriLog = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Error()
            .WriteTo.Async(a => a.File("./OctaneLog.txt"))
            .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Sixteen))
            .CreateLogger();
        var factory = LoggerFactory.Create(logging =>
        {
            logging.AddSerilog(seriLog);
        });
        
        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        var configRoot = builder.Build();

        var pauseTokenSource = new PauseTokenSource();

        using var cancelTokenSource = new CancellationTokenSource();
        var containerBuilder = new ContainerBuilder();
        containerBuilder.RegisterInstance(factory).As<ILoggerFactory>();
        containerBuilder.RegisterInstance(configRoot).As<IConfiguration>();
        containerBuilder.AddOctane();
        var engineContainer = containerBuilder.Build();
        var engine = engineContainer.Resolve<IEngine>();
        engine.DownloadFile("", "", pauseTokenSource, cancelTokenSource);
    }
}