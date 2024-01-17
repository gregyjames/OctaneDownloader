using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester.Examples;

public class NoAutofac
{
    public void NoAutoFacExample()
    {
        #region Logging Example
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
        #endregion

        #region Configuration Loading
        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        var configRoot = builder.Build();
        #endregion

        EngineBuilder.Build(factory, configRoot);
    }
}