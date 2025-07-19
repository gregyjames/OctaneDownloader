using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngineCore;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OctaneTestProject
{
    [TestFixture]
    public class EngineBuilderTest
    {
        private PauseTokenSource _pauseTokenSource;
        private CancellationTokenSource _cancelTokenSource;
        private ILogger _log;
        private ILoggerFactory _factory;
        readonly string _outFile = Path.GetRandomFileName();
        
        [SetUp]
        public void Init()
        {
            _log = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.File("./OctaneLog.txt")
                .WriteTo.Console()
                .CreateLogger();

            _factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(_log);
            });
            
            _pauseTokenSource = new PauseTokenSource(_factory);
            _cancelTokenSource = new CancellationTokenSource();
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
                if (File.Exists(_outFile))
                    File.Delete(_outFile);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void EngineBuilder_CreateWithDefaultConfig_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing EngineBuilder with default configuration");
            
            var engine = EngineBuilder.Create().Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
        }

        [Test]
        public void EngineBuilder_CreateWithActionConfig_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing EngineBuilder with action configuration");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 4;
                config.BufferSize = 16384;
                config.ShowProgress = false;
                config.NumRetries = 5;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = true;
            }).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
        }

        [Test]
        public void EngineBuilder_CreateWithLogger_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing EngineBuilder with logger");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
        }

        [Test]
        public void EngineBuilder_WithConfigurationObject_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing EngineBuilder with configuration object");
            
            var config = new OctaneConfiguration
            {
                Parts = 3,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 10,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            
            var engine = EngineBuilder.Create()
                .WithConfiguration(config)
                .WithLogger(_factory)
                .Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
        }

        [Test]
        public void EngineBuilder_WithCustomHttpClient_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing EngineBuilder with custom HTTP client");
            
            var customHttpClient = new System.Net.Http.HttpClient();
            customHttpClient.Timeout = TimeSpan.FromMinutes(5);
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            })
            .WithHttpClient(customHttpClient)
            .WithLogger(_factory)
            .Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
        }
    }
} 