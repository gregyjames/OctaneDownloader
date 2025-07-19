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
    public class ProgressBarTest
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
        public void ProgressBar_ShowProgressEnabled_ShouldCallProgressCallback()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing progress bar with ShowProgress enabled");
            
            var progressCallCount = 0;
            var doneCallCount = 0;
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = true; // Enable progress bar
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetProgressCallback(progress =>
            {
                progressCallCount++;
                Console.WriteLine($"Progress: {progress:P2}");
                Assert.That(progress, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(progress, Is.LessThanOrEqualTo(1.0));
            });
            
            engine.SetDoneCallback(success =>
            {
                doneCallCount++;
                Console.WriteLine($"Done! Success: {success}");
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(success, Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(progressCallCount, Is.GreaterThan(0), "Progress callback should be called");
            Assert.That(doneCallCount, Is.EqualTo(1), "Done callback should be called exactly once");
        }

        [Test]
        public void ProgressBar_ShowProgressDisabled_ShouldStillCallProgressCallback()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing progress callback with ShowProgress disabled");
            
            var progressCallCount = 0;
            var doneCallCount = 0;
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false; // Disable progress bar
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            engine.SetProgressCallback(progress =>
            {
                progressCallCount++;
                Console.WriteLine($"Progress: {progress:P2}");
                Assert.That(progress, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(progress, Is.LessThanOrEqualTo(1.0));
            });
            
            engine.SetDoneCallback(success =>
            {
                doneCallCount++;
                Console.WriteLine($"Done! Success: {success}");
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(success, Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(progressCallCount, Is.GreaterThan(0), "Progress callback should be called even when progress bar is disabled");
            Assert.That(doneCallCount, Is.EqualTo(1), "Done callback should be called exactly once");
        }

        [Test]
        public void ProgressBar_NoCallbacks_ShouldWorkNormally()
        {
            const string url = @"https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png";

            _log.Information("Testing progress bar without callbacks");
            
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = true;
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }, _factory).Build();
            
            Assert.That(engine, Is.Not.Null);
            
            // No callbacks set
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(File.Exists(_outFile), Is.True, "File should be downloaded successfully");
        }
    }
} 