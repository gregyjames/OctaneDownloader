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
    public class EngineBuilderTest
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
        public void EngineBuilder_CreateWithDefaultConfig_ShouldWork()
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
                Console.WriteLine("Done!");
                doneCalled = true;
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void EngineBuilder_CreateWithActionConfig_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing EngineBuilder with action configuration");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 16384,
                ShowProgress = false,
                NumRetries = 5,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = true
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                Console.WriteLine("Done!");
                doneCalled = true;
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void EngineBuilder_CreateWithLogger_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing EngineBuilder with logger");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
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
                Console.WriteLine("Done!");
                doneCalled = true;
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void EngineBuilder_WithConfigurationObject_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

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
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                Console.WriteLine("Done!");
                doneCalled = true;
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void EngineBuilder_WithCustomHttpClient_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing EngineBuilder with custom HTTP client");
            
            // Build mock client with specific timeout setting to verify custom client pass-through
            using var mockHandler = new MockHttpMessageHandler(_mockData);
            using var customHttpClient = new HttpClient(mockHandler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, customHttpClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                Console.WriteLine("Done!");
                doneCalled = true;
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }
    }
}