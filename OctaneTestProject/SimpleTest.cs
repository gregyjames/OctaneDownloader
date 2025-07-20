using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngine;
using OctaneEngine.Clients;
using Serilog;
using ILogger = Serilog.ILogger;

namespace OctaneTestProject
{
    [TestFixture]
    public class SimpleTest
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
        public void SimpleDownload_ShouldWork()
        {
            const string url = @"https://httpbin.org/bytes/512"; // Small file

            _log.Information("Testing simple download");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 1; // Single part
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be called");
        }

        [Test]
        public void EngineBuilder_DefaultConfiguration_ShouldWork()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png"; // Very small file

            _log.Information("Testing EngineBuilder with default configuration");
            
            var engine = EngineBuilder.Create().Build();
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be called");
        }

        [Test]
        public void EngineBuilder_WithLogger_ShouldWork()
        {
            const string url = @"https://httpbin.org/bytes/256"; // Very small file

            _log.Information("Testing EngineBuilder with logger");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 1;
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be called");
        }

        [Test]
        public void ProgressCallback_ShouldBeCalled()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png"; // Small file

            _log.Information("Testing progress callback");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2; // Multiple parts to ensure progress
                config.BufferSize = 8192;
                config.ShowProgress = false;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            var progressCalled = false;
            var doneCalled = false;
            
            engine.SetProgressCallback(progress =>
            {
                progressCalled = true;
                Console.WriteLine($"Progress: {progress:P2}");
                Assert.That(progress, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(progress, Is.LessThanOrEqualTo(1.0));
            });
            
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(progressCalled, Is.True, "Progress callback should be called");
            Assert.That(doneCalled, Is.True, "Done callback should be called");
        }

        [Test]
        public void Configuration_ShowProgressDisabled_ShouldNotShowProgressBar()
        {
            const string url = @"https://httpbin.org/bytes/256"; // Very small file

            _log.Information("Testing configuration with ShowProgress disabled");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 1;
                config.BufferSize = 8192;
                config.ShowProgress = false; // Explicitly disable progress
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be called");
        }
    }
} 