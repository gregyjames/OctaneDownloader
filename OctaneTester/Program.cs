using System.IO;
using System.Threading;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester
{
    internal static class Program
    {
        private const string Url = "https://imgv3.fotor.com/images/blog-cover-image/what-is-png-file-cover-with-blue-background.jpg";
        private static void Main()
        {
            #region Logging Configuration
            var seriLog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
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

            #region Find and Set optimal number of parts
            //var optimalNumberOfParts = Engine.GetOptimalNumberOfParts(Url).Result;
            //seriLog.Information("Optimal number of parts to download file: {OptimalNumberOfParts}", optimalNumberOfParts);
            #endregion
            
            //seriLog.Information("Speed: {Result}", NetworkAnalyzer.GetCurrentNetworkSpeed().Result);
            //seriLog.Information("Latency: {Result}", NetworkAnalyzer.GetCurrentNetworkLatency().Result);
            var pauseTokenSource = new PauseTokenSource();
            var cancelTokenSource = new CancellationTokenSource();
            
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterInstance(factory).As<ILoggerFactory>();
            containerBuilder.RegisterInstance(configRoot).As<IConfiguration>();
            containerBuilder.RegisterModule(new EngineModule(Url));
            var engineContainer = containerBuilder.Build();
            var engine = engineContainer.Resolve<IEngine>();
            engine.DownloadFile(Url, null, pauseTokenSource, cancelTokenSource).Wait();

        }
    }
}