// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace BenchmarkOctaneProject
{
    public class Program
    {
        private HttpClient _client = null;
        private Engine _OctaneEngine = null;
        private OctaneConfiguration config;
        private const string Url = "https://ash-speed.hetzner.com/100MB.bin";

        [GlobalSetup]
        public void GlobalSetup()
        {
            _client = new HttpClient();
            var seriLog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Error()
                .WriteTo.Async(a => a.Console(theme: AnsiConsoleTheme.Sixteen))
                .CreateLogger();
            var factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(seriLog);
            });

            #region Configuration Loading
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true);
            var configRoot = builder.Build();
            config = new OctaneConfiguration(configRoot, factory);
            #endregion

            _OctaneEngine = new Engine(null, config);
        }

        [Benchmark]
        public async Task BenchmarkOctane()
        {
            var pauseTokenSource = new PauseTokenSource();
            var cancelTokenSource = new CancellationTokenSource();
            await _OctaneEngine.DownloadFile(Url, "output.zip", pauseTokenSource, cancelTokenSource);
        }

        [Benchmark]
        public async Task BenchmarkHttpClient()
        {
            //Write your code here   
            var message = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, Url));
            var stream = await message.Content.ReadAsStreamAsync();
            var fileStream = new FileStream(@"output2.zip", FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            //factory.Dispose();
            _client.Dispose();
        }
        
        public static void Main()
        {
            var summary = BenchmarkRunner.Run<Program>();
        }
    }
}