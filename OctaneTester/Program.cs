using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester
{
    internal static class Program
    {
        private const string Url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
        
        private static void Main()
        {
            #region Logging Configuration
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

            #region Find and Set optimal number of parts
            //var optimalNumberOfParts = Engine.GetOptimalNumberOfParts(Url).Result;
            //seriLog.Information("Optimal number of parts to download file: {OptimalNumberOfParts}", optimalNumberOfParts);
            #endregion
            
            //seriLog.Information("Speed: {Result}", NetworkAnalyzer.GetCurrentNetworkSpeed().Result);
            //seriLog.Information("Latency: {Result}", NetworkAnalyzer.GetCurrentNetworkLatency().Result);
            var pauseTokenSource = new PauseTokenSource();
            using var cancelTokenSource = new CancellationTokenSource();
            var containerBuilder = new ContainerBuilder();
            containerBuilder.RegisterInstance(factory).As<ILoggerFactory>();
            containerBuilder.RegisterInstance(configRoot).As<IConfiguration>();
            containerBuilder.AddOctane();
            var engineContainer = containerBuilder.Build();
            var engine = engineContainer.Resolve<IEngine>();
            engine.DownloadFile(new OctaneRequest()
            {
                URL = Url,
                Headers = new Dictionary<string, string>()
                {
                    
                }
            }, null, pauseTokenSource, cancelTokenSource).Wait();
        }
    }
}
