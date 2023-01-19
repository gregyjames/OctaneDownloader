using System;
using System.Globalization;
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
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                BytesPerSecond = 1,
                UseProxy = false,
                Proxy = null,
                DoneCallback = x => { Console.WriteLine("Done!"); },
                ProgressCallback = x => { Console.WriteLine(x.ToString(CultureInfo.InvariantCulture)); },
                NumRetries = 10
            };

            var seriLog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Error()
                .WriteTo.File("./OctaneLog.txt")
                .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
                .CreateLogger();

            var factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(seriLog);
            });
            
            var pauseTokenSource = new PauseTokenSource(factory);
            
            Engine.DownloadFile("https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png", factory, null, config, pauseTokenSource).Wait();
        }
    }
}