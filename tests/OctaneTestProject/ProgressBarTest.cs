using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using OctaneEngineCore.Implementations;
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
        private string _outFile;
        private byte[] _mockData;
        
        [SetUp]
        public void Init()
        {
            _outFile = Path.GetRandomFileName();
            _log = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger();

            _factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(_log);
            });
            
            _pauseTokenSource = new PauseTokenSource(_factory);
            _cancelTokenSource = new CancellationTokenSource();

            _mockData = new byte[1024 * 30]; // 30 KB
            new Random().NextBytes(_mockData);
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
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing progress bar with ShowProgress enabled");
            
            var progressCallCount = 0;
            var doneCallCount = 0;
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = true, // Enable progress bar
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
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
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing progress callback with ShowProgress disabled");
            
            var progressCallCount = 0;
            var doneCallCount = 0;
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false, // Disable progress bar
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
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
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing progress bar without callbacks");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = true,
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            // No callbacks set
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(File.Exists(_outFile), Is.True, "File should be downloaded successfully");
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }
    }
}