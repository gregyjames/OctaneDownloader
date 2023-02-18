using System;
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
        private const String Url = @"https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
        private static void Main()
        {
            var config = new OctaneConfiguration
            {
                Parts = Environment.ProcessorCount,
                BufferSize = 2097152,
                ShowProgress = false,
                BytesPerSecond = 1,
                UseProxy = false,
                Proxy = null,
                DoneCallback = _ => {  },
                ProgressCallback = _ => {  },
                NumRetries = 10
            };
            
            
            //SERILOG EXAMPLE 
            var seriLog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.File("./OctaneLog.txt")
                .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
                .CreateLogger();

            
            var factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(seriLog);
            });
            
            var pauseTokenSource = new PauseTokenSource();
            var cancelTokenSource = new CancellationTokenSource();
            
            Engine.DownloadFile(Url, factory, null, config, pauseTokenSource, cancelTokenSource).Wait(cancelTokenSource.Token);
        }
    }
}