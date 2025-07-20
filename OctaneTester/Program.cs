using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace OctaneTester
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder(args).UseSerilog((context, configuration) =>
            {
                configuration
                    .Enrich.FromLogContext()
                    .MinimumLevel.Verbose()
                    .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Sixteen));
            }).ConfigureAppConfiguration(configurationBuilder =>
            {
                configurationBuilder.AddJsonFile("appsettings.json");
            }).UseOctaneEngine().ConfigureServices(collection =>
            {
                collection.AddHostedService<DownloadService>();
            }).RunConsoleAsync();
        }
    }
}
