// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using IEngine = OctaneEngineCore.IEngine;

namespace BenchmarkOctaneProject
{
    public class Program
    {
        private HttpClient? _client;
        private IEngine? _octaneEngine;
        private IEngine? _octaneEngine2;
        private PauseTokenSource? _pauseTokenSource;
        private CancellationTokenSource? _cancelTokenSource;
        [Params("http://link.testfile.org/150MB", "https://link.testfile.org/250MB", "https://link.testfile.org/500MB")]
        public string? Url;

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
            
            _octaneEngine = EngineBuilder.Create().WithConfiguration(_ =>
            {
                
            }).WithLogger(factory).Build();
            
            _octaneEngine2 = EngineBuilder.Create().WithConfiguration(configuration =>
            {
                configuration.LowMemoryMode = true;
            }).WithLogger(factory).Build();

            _pauseTokenSource = new PauseTokenSource();
            _cancelTokenSource = new CancellationTokenSource();
        }
        
        [Benchmark]
        public async Task BenchmarkOctane()
        {
            await _octaneEngine?.DownloadFile(new OctaneRequest(Url, "output0.zip"), _pauseTokenSource, _cancelTokenSource!.Token)!;
        }

        [Benchmark]
        public async Task BenchmarkOctaneLowMemory()
        {
            await _octaneEngine2?.DownloadFile(new OctaneRequest(Url, "output1.zip"), _pauseTokenSource, _cancelTokenSource!.Token)!;
        }
        [Benchmark]
        public async Task BenchmarkHttpClient()
        {
            //Write your code here   
            var message = await _client?.SendAsync(new HttpRequestMessage(HttpMethod.Get, Url))!;
            var stream = await message.Content.ReadAsStreamAsync();
            var fileStream = new FileStream(@"output2.zip", FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream);
        }
        [GlobalCleanup]
        public void GlobalCleanup()
        {
            //factory.Dispose();
            _client?.Dispose();
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
            BenchmarkRunner.Run<Program>(config);
        }
    }
}