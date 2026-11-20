using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
    public class ConfigurationTest
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

            // Set up 100 KB of random mock data for configuration tests
            _mockData = new byte[1024 * 100];
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
        public void Configuration_ShowProgressDisabled_ShouldNotShowProgressBar()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing configuration with ShowProgress disabled");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
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
                Console.WriteLine("Done!");
                doneCalled = true;
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                
                var downloadedData = File.ReadAllBytes(_outFile);
                Assert.That(downloadedData, Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void Configuration_ShowProgressEnabled_ShouldShowProgressBar()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing configuration with ShowProgress enabled");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = true, // Explicitly enable progress
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
                
                var downloadedData = File.ReadAllBytes(_outFile);
                Assert.That(downloadedData, Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void Configuration_LowMemoryMode_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing configuration with LowMemoryMode enabled");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 3,
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = true // Enable low memory mode
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
                
                var downloadedData = File.ReadAllBytes(_outFile);
                Assert.That(downloadedData, Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void Configuration_DifferentBufferSizes_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing configuration with different buffer sizes");
            
            var bufferSizes = new[] { 4096, 8192, 16384, 32768 };
            
            foreach (var bufferSize in bufferSizes)
            {
                using var mockClient = Helpers.GetMockHttpClient(_mockData);
                
                var config = new OctaneConfiguration
                {
                    Parts = 2,
                    BufferSize = bufferSize,
                    ShowProgress = false,
                    NumRetries = 3,
                    BytesPerSecond = 1,
                    UseProxy = false,
                    LowMemoryMode = false
                };
                var engine = new Engine(config, mockClient, _factory);
                
                Assert.That(engine, Is.Not.Null);
                
                var testFile = Path.GetRandomFileName();
                var doneCalled = false;
                
                engine.SetDoneCallback(success =>
                {
                    Console.WriteLine($"Done with buffer size {bufferSize}!");
                    doneCalled = true;
                    Assert.That(success, Is.True);
                    Assert.That(File.Exists(testFile), Is.True);
                    
                    var downloadedData = File.ReadAllBytes(testFile);
                    Assert.That(downloadedData, Is.EqualTo(_mockData));
                    
                    File.Delete(testFile);
                });
                
                engine.DownloadFile(new OctaneRequest(url, testFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
                Assert.That(doneCalled, Is.True);
            }
        }

        [Test]
        public void Configuration_DifferentPartCounts_ShouldWork()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing configuration with different part counts");
            
            var partCounts = new[] { 1, 2, 4, 8 };
            
            foreach (var parts in partCounts)
            {
                using var mockClient = Helpers.GetMockHttpClient(_mockData);
                
                var config = new OctaneConfiguration
                {
                    Parts = parts,
                    BufferSize = 8192,
                    ShowProgress = false,
                    NumRetries = 3,
                    BytesPerSecond = 1,
                    UseProxy = false,
                    LowMemoryMode = false
                };
                var engine = new Engine(config, mockClient, _factory);
                
                Assert.That(engine, Is.Not.Null);
                
                var testFile = Path.GetRandomFileName();
                var doneCalled = false;
                
                engine.SetDoneCallback(success =>
                {
                    Console.WriteLine($"Done with {parts} parts!");
                    doneCalled = true;
                    Assert.That(success, Is.True);
                    Assert.That(File.Exists(testFile), Is.True);
                    
                    var downloadedData = File.ReadAllBytes(testFile);
                    Assert.That(downloadedData, Is.EqualTo(_mockData));
                    
                    File.Delete(testFile);
                });
                
                engine.DownloadFile(new OctaneRequest(url, testFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
                Assert.That(doneCalled, Is.True);
            }
        }

        private sealed class CapturingDelegatingHandler : DelegatingHandler
        {
            public ConcurrentBag<(HttpMethod Method, Version Version, HttpVersionPolicy Policy)> Captured { get; } = new();

            public CapturingDelegatingHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Captured.Add((request.Method, request.Version, request.VersionPolicy));
                return base.SendAsync(request, cancellationToken);
            }
        }

        [Test]
        public void Configuration_DefaultHttpVersion_UsesHttp11AndRequestVersionOrLower()
        {
            const string url = @"https://mockurl.com/file.png";
            using var capturingHandler = new CapturingDelegatingHandler(new MockHttpMessageHandler(_mockData));
            using var mockClient = new HttpClient(capturingHandler);

            var config = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 8192,
                ShowProgress = false,
                LowMemoryMode = false
            };

            Assert.That(config.HttpVersion, Is.EqualTo(HttpVersion.Version11));
            Assert.That(config.HttpVersionPolicy, Is.EqualTo(HttpVersionPolicy.RequestVersionOrLower));

            var engine = new Engine(config, mockClient, _factory);
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();

            Assert.That(capturingHandler.Captured, Is.Not.Empty);
            foreach (var entry in capturingHandler.Captured)
            {
                Assert.That(entry.Version, Is.EqualTo(HttpVersion.Version11));
                Assert.That(entry.Policy, Is.EqualTo(HttpVersionPolicy.RequestVersionOrLower));
            }
        }

        [Test]
        public void Configuration_CustomHttpVersion_PropagatesToRequests()
        {
            const string url = @"https://mockurl.com/file.png";
            using var capturingHandler = new CapturingDelegatingHandler(new MockHttpMessageHandler(_mockData));
            using var mockClient = new HttpClient(capturingHandler);

            var config = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 8192,
                ShowProgress = false,
                LowMemoryMode = false,
                HttpVersion = HttpVersion.Version20,
                HttpVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };

            var engine = new Engine(config, mockClient, _factory);
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();

            Assert.That(capturingHandler.Captured, Is.Not.Empty);
            foreach (var entry in capturingHandler.Captured)
            {
                Assert.That(entry.Version, Is.EqualTo(HttpVersion.Version20));
                Assert.That(entry.Policy, Is.EqualTo(HttpVersionPolicy.RequestVersionOrHigher));
            }
        }
    }
}