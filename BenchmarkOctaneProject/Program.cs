// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Net.Http;

namespace BenchmarkOctaneProject
{
    public class Program
    {
        private HttpClient _client = null;
        private OctaneEngine.Engine _OctaneEngine = null;
        private OctaneEngine.Engine _OctaneEngine2 = null;
        private PauseTokenSource pauseTokenSource;
        private CancellationTokenSource cancelTokenSource;
        private OctaneConfiguration config;
        [Params("http://link.testfile.org/150MB", "https://link.testfile.org/250MB", "https://link.testfile.org/500MB")]
        public string Url;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _client = new HttpClient();
            _client.Timeout = Timeout.InfiniteTimeSpan;
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

            _OctaneEngine = new OctaneEngine.Engine(null, config);

            config.LowMemoryMode = true;
            _OctaneEngine2 = new OctaneEngine.Engine(null, config);

            pauseTokenSource = new PauseTokenSource();
            cancelTokenSource = new CancellationTokenSource();
        }
        
        [Benchmark]
        public async Task BenchmarkOctane()
        {
            await _OctaneEngine.DownloadFile(Url, "output0.zip", pauseTokenSource, cancelTokenSource);
        }

        [Benchmark]
        public async Task BenchmarkOctaneLowMemory()
        {
            await _OctaneEngine2.DownloadFile(Url, "output1.zip", pauseTokenSource, cancelTokenSource);
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
            var config = ManualConfig.CreateEmpty() // A configuration for our benchmarks
                 .AddLogger(new ConsoleLogger())
                 .AddExporter(DefaultConfig.Instance.GetExporters().ToArray())
                 .AddColumnProvider(DefaultConfig.Instance.GetColumnProviders().ToArray())
                 .AddJob(Job.Default // Adding first job
                     .WithPlatform(Platform.X64) // Run as x64 application
                     .WithIterationCount(5)
                     .WithWarmupCount(0)
            );
            var summary = BenchmarkRunner.Run<Program>(config);
        }
    }
}