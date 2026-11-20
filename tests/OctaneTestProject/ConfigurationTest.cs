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

        [Test]
        public void Configuration_DynamicChunking_RespectsMinPartSizeAndMaxChunkSize()
        {
            const string url = @"https://mockurl.com/file.png";
            using var capturingHandler = new CapturingDelegatingHandler(new MockHttpMessageHandler(_mockData));
            using var mockClient = new HttpClient(capturingHandler);

            // 100 KB file with default MinPartSize = 1 MB should issue 1 GET (not 8)
            var smallConfig = new OctaneConfiguration
            {
                Parts = 8,
                BufferSize = 8192,
                ShowProgress = false,
                LowMemoryMode = false
            };

            var smallEngine = new Engine(smallConfig, mockClient, _factory);
            smallEngine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();

            int smallGetCount = 0;
            foreach (var entry in capturingHandler.Captured)
            {
                if (entry.Method == HttpMethod.Get) smallGetCount++;
            }
            Assert.That(smallGetCount, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));

            // 100 KB file with MinPartSize = 4 KB and MaxChunkSize = 16 KB should split into ceil(100/16) = 7 chunks
            using var chunkHandler = new CapturingDelegatingHandler(new MockHttpMessageHandler(_mockData));
            using var chunkClient = new HttpClient(chunkHandler);
            var chunkConfig = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 8192,
                MinPartSize = 4 * 1024,
                MaxChunkSize = 16 * 1024,
                ShowProgress = false,
                LowMemoryMode = false
            };

            var chunkEngine = new Engine(chunkConfig, chunkClient, _factory);
            chunkEngine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();

            int dynamicGetCount = 0;
            foreach (var entry in chunkHandler.Captured)
            {
                if (entry.Method == HttpMethod.Get) dynamicGetCount++;
            }
            Assert.That(dynamicGetCount, Is.EqualTo(7));
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }

        [Test]
        public void FastStart_EliminatesHeadRequest_ForRangeAndNonRangeServers()
        {
            const string url = @"https://mockurl.com/file.png";

            // Range-supported server (206 Partial Content): 0 HEAD requests
            using var rangeHandler = new CapturingDelegatingHandler(new MockHttpMessageHandler(_mockData, supportsRange: true));
            using var rangeClient = new HttpClient(rangeHandler);
            var rangeConfig = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 8192,
                MinPartSize = 4 * 1024,
                MaxChunkSize = 32 * 1024,
                ShowProgress = false
            };

            var rangeEngine = new Engine(rangeConfig, rangeClient, _factory);
            rangeEngine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();

            Assert.That(rangeHandler.Captured, Has.None.Matches<(HttpMethod Method, Version, HttpVersionPolicy)>(e => e.Method == HttpMethod.Head));
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));

            // Non-range server (200 OK): 0 HEAD requests and exactly 1 GET request
            using var nonRangeHandler = new CapturingDelegatingHandler(new MockHttpMessageHandler(_mockData, supportsRange: false));
            using var nonRangeClient = new HttpClient(nonRangeHandler);
            var nonRangeEngine = new Engine(rangeConfig, nonRangeClient, _factory);
            nonRangeEngine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();

            Assert.That(nonRangeHandler.Captured, Has.None.Matches<(HttpMethod Method, Version, HttpVersionPolicy)>(e => e.Method == HttpMethod.Head));
            Assert.That(nonRangeHandler.Captured.Count, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }

        private sealed class StallingFirstAttemptHandler : DelegatingHandler
        {
            private int _stalledOnce;
            public int HedgedRetryObserved { get; private set; }
            private long _stalledRangeFrom = -1;

            public StallingFirstAttemptHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                long from = request.Headers.Range?.Ranges.GetEnumerator() is var e && e.MoveNext()
                    ? (e.Current.From ?? 0)
                    : 0;

                // Stall the first attempt of a non-zero chunk for 5 seconds; let the hedged attempt succeed immediately
                if (from > 0 && Interlocked.CompareExchange(ref _stalledOnce, 1, 0) == 0)
                {
                    Volatile.Write(ref _stalledRangeFrom, from);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                else if (from > 0 && from == Volatile.Read(ref _stalledRangeFrom))
                {
                    HedgedRetryObserved = 1;
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }

        [Test]
        public void TailHedging_RacesStalledChunk_AndCompletesQuickly()
        {
            const string url = @"https://mockurl.com/file.png";
            using var stallingHandler = new StallingFirstAttemptHandler(new MockHttpMessageHandler(_mockData, supportsRange: true));
            using var client = new HttpClient(stallingHandler);

            var config = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 8192,
                MinPartSize = 4 * 1024,
                MaxChunkSize = 16 * 1024,
                EnableTailHedging = true,
                TailHedgeDelayMs = 50,
                ShowProgress = false
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var engine = new Engine(config, client, _factory);
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            sw.Stop();

            Assert.That(stallingHandler.HedgedRetryObserved, Is.EqualTo(1), "Expected an idle worker to hedge the stalled chunk");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), "Hedged download should beat the 5s stall");
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }
    }
}