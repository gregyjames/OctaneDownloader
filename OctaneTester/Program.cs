using System;
using System.Globalization;
using System.Threading;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            var config = new OctaneConfiguration
            {
                Parts = Environment.ProcessorCount,
                BufferSize = 2097152,
                ShowProgress = true,
                BytesPerSecond = 1,
                UseProxy = false,
                Proxy = null,
                DoneCallback = x => { Console.WriteLine("Done!"); },
                ProgressCallback = x => { Console.WriteLine(x.ToString(CultureInfo.InvariantCulture)); },
                NumRetries = 10
            };
            
            
            //SERILOG EXAMPLE 
            var seriLog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Fatal()
                .WriteTo.File("./OctaneLog.txt")
                .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
                .CreateLogger();

            
            var factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(seriLog);
            });
            
            var pauseTokenSource = new PauseTokenSource();
            var cancelTokenSource = new CancellationTokenSource();
            
            Engine.DownloadFile("https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ", factory, null, config, pauseTokenSource, cancelTokenSource).Wait();
        }
    }
}