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

        private sealed class PartialThenStallHandler : HttpMessageHandler
        {
            private readonly byte[] _data;
            private readonly int _stallAfterBytes;
            private int _stalledOnce;
            private long _stalledChunkStart = -1;
            private long _stalledChunkEnd = -1;
            public long StolenRangeStart { get; private set; } = -1;

            public PartialThenStallHandler(byte[] data, int stallAfterBytes)
            {
                _data = data;
                _stallAfterBytes = stallAfterBytes;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                long start = 0;
                long end = _data.Length - 1;
                if (request.Headers.Range?.Ranges.GetEnumerator() is var e && e.MoveNext())
                {
                    start = e.Current.From ?? 0;
                    end = Math.Min(e.Current.To ?? (_data.Length - 1), _data.Length - 1);
                }

                long length = end - start + 1;

                // Stall the first non-zero chunk halfway through its stream
                if (start > 0 && Interlocked.CompareExchange(ref _stalledOnce, 1, 0) == 0)
                {
                    Volatile.Write(ref _stalledChunkStart, start);
                    Volatile.Write(ref _stalledChunkEnd, end);

                    var stallingStream = new PartialStallStream(_data, (int)start, (int)length, _stallAfterBytes);
                    var resp = new HttpResponseMessage(HttpStatusCode.PartialContent)
                    {
                        Content = new StreamContent(stallingStream)
                    };
                    resp.Content.Headers.ContentLength = length;
                    resp.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(start, end, _data.Length);
                    return Task.FromResult(resp);
                }

                // Record if this is a tail-steal request for the stalled chunk
                long stalledStart = Volatile.Read(ref _stalledChunkStart);
                long stalledEnd = Volatile.Read(ref _stalledChunkEnd);
                if (stalledStart > 0 && start > stalledStart && end == stalledEnd)
                {
                    StolenRangeStart = start;
                }

                var slice = new byte[length];
                Array.Copy(_data, start, slice, 0, length);
                var ok = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(slice)
                };
                ok.Content.Headers.ContentLength = length;
                ok.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(start, end, _data.Length);
                return Task.FromResult(ok);
            }

            private sealed class PartialStallStream : Stream
            {
                private readonly byte[] _data;
                private readonly int _baseOffset;
                private readonly int _length;
                private readonly int _stallAfterBytes;
                private int _pos;

                public PartialStallStream(byte[] data, int baseOffset, int length, int stallAfterBytes)
                {
                    _data = data;
                    _baseOffset = baseOffset;
                    _length = length;
                    _stallAfterBytes = stallAfterBytes;
                }

                public override bool CanRead => true;
                public override bool CanSeek => false;
                public override bool CanWrite => false;
                public override long Length => _length;
                public override long Position { get => _pos; set => throw new NotSupportedException(); }
                public override void Flush() { }
                public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
                public override void SetLength(long value) => throw new NotSupportedException();
                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

                public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                {
                    if (_pos >= _length) return 0;

                    if (_pos < _stallAfterBytes)
                    {
                        int toCopy = Math.Min(buffer.Length, _stallAfterBytes - _pos);
                        _data.AsSpan(_baseOffset + _pos, toCopy).CopyTo(buffer.Span);
                        _pos += toCopy;
                        return toCopy;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    int remaining = Math.Min(buffer.Length, _length - _pos);
                    _data.AsSpan(_baseOffset + _pos, remaining).CopyTo(buffer.Span);
                    _pos += remaining;
                    return remaining;
                }
            }
        }

        [Test]
        public void TailHedging_StealsOnlyRemainingTailBytes_WhenSlowChunkHasPartialProgress()
        {
            const string url = @"https://mockurl.com/file.png";
            using var handler = new PartialThenStallHandler(_mockData, stallAfterBytes: 8 * 1024);
            using var client = new HttpClient(handler);

            var config = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 8192,
                MinPartSize = 4 * 1024,
                MaxChunkSize = 16 * 1024,
                LowMemoryMode = true, // Commits every 8 KB buffer immediately
                EnableTailHedging = true,
                TailHedgeDelayMs = 80,
                ShowProgress = false
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var engine = new Engine(config, client, _factory);
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            sw.Stop();

            Assert.That(handler.StolenRangeStart, Is.GreaterThan(0), "Expected idle worker to steal only the uncommitted tail bytes");
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), "Tail steal should beat the 5s stall");
            Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
        }

        [Test]
        public void Configuration_DefaultParts_IsClampedBetween4And8_AndAllowsOverride()
        {
            var defaultConfig = new OctaneConfiguration();
            Assert.That(defaultConfig.Parts, Is.InRange(4, 8));
            Assert.That(defaultConfig.Parts, Is.EqualTo(Math.Clamp(Environment.ProcessorCount, 4, 8)));

            // Explicit user override above 8 should be preserved
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            var customConfig = new OctaneConfiguration { Parts = 16 };
            using var engine = new Engine(customConfig, mockClient, _factory);
            Assert.That(customConfig.Parts, Is.EqualTo(16));

            // Non-positive Parts should normalize to DefaultParts
            var invalidConfig = new OctaneConfiguration { Parts = 0 };
            using var normalizedEngine = new Engine(invalidConfig, mockClient, _factory);
            Assert.That(invalidConfig.Parts, Is.InRange(4, 8));
        }
    }
}