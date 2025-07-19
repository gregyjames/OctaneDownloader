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
    public class ConfigurationTest
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
        public void Configuration_ShowProgressDisabled_ShouldNotShowProgressBar()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing configuration with ShowProgress disabled");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false; // Explicitly disable progress
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
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource).Wait();
        }

        [Test]
        public void Configuration_ShowProgressEnabled_ShouldShowProgressBar()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing configuration with ShowProgress enabled");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = true; // Explicitly enable progress
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
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource).Wait();
        }

        [Test]
        public void Configuration_LowMemoryMode_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing configuration with LowMemoryMode enabled");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = true; // Enable low memory mode
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetDoneCallback(_ =>
            {
                Console.WriteLine("Done!");
                Assert.That(File.Exists(_outFile), Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource).Wait();
        }

        [Test]
        public void Configuration_DifferentBufferSizes_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing configuration with different buffer sizes");
            
            var bufferSizes = new[] { 4096, 8192, 16384, 32768 };
            
            foreach (var bufferSize in bufferSizes)
            {
                var engine = EngineBuilder.Create(config =>
                {
                    config.Parts = 2;
                    config.BufferSize = bufferSize;
                    config.ShowProgress = false;
                    config.NumRetries = 3;
                    config.BytesPerSecond = 1;
                    config.UseProxy = false;
                    config.LowMemoryMode = false;
                }, _factory).Build();
                
                Assert.That(engine, Is.Not.Null);
                
                var testFile = Path.GetRandomFileName();
                
                engine.SetDoneCallback(_ =>
                {
                    Console.WriteLine($"Done with buffer size {bufferSize}!");
                    Assert.That(File.Exists(testFile), Is.True);
                    File.Delete(testFile);
                });
                
                engine.DownloadFile(new OctaneRequest(url, testFile), _pauseTokenSource, _cancelTokenSource).Wait();
            }
        }

        [Test]
        public void Configuration_DifferentPartCounts_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing configuration with different part counts");
            
            var partCounts = new[] { 1, 2, 4, 8 };
            
            foreach (var parts in partCounts)
            {
                var engine = EngineBuilder.Create(config =>
                {
                    config.Parts = parts;
                    config.BufferSize = 8192;
                    config.ShowProgress = false;
                    config.NumRetries = 3;
                    config.BytesPerSecond = 1;
                    config.UseProxy = false;
                    config.LowMemoryMode = false;
                }, _factory).Build();
                
                Assert.That(engine, Is.Not.Null);
                
                var testFile = Path.GetRandomFileName();
                
                engine.SetDoneCallback(_ =>
                {
                    Console.WriteLine($"Done with {parts} parts!");
                    Assert.That(File.Exists(testFile), Is.True);
                    File.Delete(testFile);
                });
                
                engine.DownloadFile(new OctaneRequest(url, testFile), _pauseTokenSource, _cancelTokenSource).Wait();
            }
        }
    }
} 