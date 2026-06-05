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
    public class SimpleTest
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
                .WriteTo.File($"./{_outFile}.log")
                .WriteTo.Console()
                .CreateLogger();

            _factory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(_log);
            });
            
            _pauseTokenSource = new PauseTokenSource(_factory);
            _cancelTokenSource = new CancellationTokenSource();

            _mockData = new byte[1024 * 10]; // 10 KB
            new Random().NextBytes(_mockData);
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
                if (File.Exists(_outFile))
                    File.Delete(_outFile);
                
                var logFile = $"./{_outFile}.log";
                if (File.Exists(logFile))
                    File.Delete(logFile);
            }
            catch
            {
                // ignored
            }
        }

        [Test]
        public void SimpleDownload_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing simple download");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 1, // Single part
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
                Assert.That(success, Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be called");
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }

        [Test]
        public void EngineBuilder_DefaultConfiguration_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing EngineBuilder with default configuration");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = Environment.ProcessorCount,
                BufferSize = 8192,
                NumRetries = 3,
                BytesPerSecond = 1
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
                Assert.That(success, Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be called");
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }

        [Test]
        public void EngineBuilder_WithLogger_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing EngineBuilder with logger");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 1,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
                Assert.That(success, Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be called");
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }

        [Test]
        public void ProgressCallback_ShouldBeCalled()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing progress callback");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2, // Multiple parts to ensure progress
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
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
                Assert.That(success, Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(progressCalled, Is.True, "Progress callback should be called");
            Assert.That(doneCalled, Is.True, "Done callback should be called");
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }

        [Test]
        public void Configuration_ShowProgressDisabled_ShouldNotShowProgressBar()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing configuration with ShowProgress disabled");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 1,
                BufferSize = 8192,
                ShowProgress = false, // Explicitly disable progress
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine($"Download completed with success: {success}");
                Assert.That(success, Is.True);
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            
            Assert.That(doneCalled, Is.True, "Done callback should be called");
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }
    }
}