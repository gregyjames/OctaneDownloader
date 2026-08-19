using System;
using System.IO;
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
    public class ErrorHandlingTest
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

            _mockData = new byte[1024 * 20]; // 20 KB
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
        public void ErrorHandling_InvalidUrl_ShouldThrowException()
        {
            const string invalidUrl = @"https://invalid-domain.com/file.zip";

            _log.Information("Testing error handling with invalid URL");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData, shouldFail: true);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 1, 
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var exceptionThrown = false;
            
            try
            {
                engine.DownloadFile(new OctaneRequest(invalidUrl, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            }
            catch (Exception)
            {
                exceptionThrown = true;
            }
            
            Assert.That(exceptionThrown, Is.True, "Should throw exception for invalid URL");
        }

        [Test]
        public void ErrorHandling_NonExistentFile_ShouldThrowException()
        {
            const string nonExistentUrl = @"https://mockurl.com/status/404";

            _log.Information("Testing error handling with non-existent file");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData, shouldFail: true);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 1, 
                BytesPerSecond = 1,
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var exceptionThrown = false;
            
            try
            {
                engine.DownloadFile(new OctaneRequest(nonExistentUrl, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            }
            catch (Exception)
            {
                exceptionThrown = true;
            }
            
            Assert.That(exceptionThrown, Is.True, "Should throw exception for non-existent file");
        }

        [Test]
        public void ErrorHandling_Cancellation_ShouldCancelDownload()
        {
            const string url = @"https://mockurl.com/delay/10"; 

            _log.Information("Testing error handling with cancellation");
            
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
            
            var cancellationRequested = false;
            
            // Cancel after 100 ms
            var timer = new System.Threading.Timer(_ =>
            {
                _cancelTokenSource.Cancel();
                cancellationRequested = true;
            }, null, 100, Timeout.Infinite);
            
            var exceptionThrown = false;
            
            try
            {
                engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            }
            catch (OperationCanceledException)
            {
                exceptionThrown = true;
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException || ex.InnerException is TaskCanceledException)
            {
                exceptionThrown = true;
            }
            
            timer.Dispose();
            
            Assert.That(cancellationRequested, Is.True, "Cancellation should be requested");
            Assert.That(exceptionThrown, Is.True, "Should throw cancellation exception");
        }

        [Test]
        public void ErrorHandling_ZeroParts_ShouldUseDefault()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing error handling with zero parts");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 0, // Invalid value
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
                Console.WriteLine("Done with zero parts!");
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void ErrorHandling_ZeroBufferSize_ShouldUseDefault()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing error handling with zero buffer size");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 0, // Invalid value
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
                Console.WriteLine("Done with zero buffer size!");
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public void ErrorHandling_InvalidConfiguration_ShouldHandleGracefully()
        {
            const string url = @"https://mockurl.com/file.png";

            _log.Information("Testing error handling with invalid configuration");
            
            using var mockClient = Helpers.GetMockHttpClient(_mockData);
            
            var config = new OctaneConfiguration
            {
                Parts = -1, // Invalid negative value
                BufferSize = -1, // Invalid negative value
                ShowProgress = false,
                NumRetries = -1, // Invalid negative value
                BytesPerSecond = -1, // Invalid negative value
                UseProxy = false,
                LowMemoryMode = false
            };
            var engine = new Engine(config, mockClient, _factory);
            
            Assert.That(engine, Is.Not.Null);
            
            var doneCalled = false;
            engine.SetDoneCallback(success =>
            {
                doneCalled = true;
                Console.WriteLine("Done with invalid configuration!");
                Assert.That(success, Is.True);
                Assert.That(File.Exists(_outFile), Is.True);
                Assert.That(File.ReadAllBytes(_outFile), Is.EqualTo(_mockData));
            });
            
            engine.DownloadFile(new OctaneRequest(url, _outFile), _pauseTokenSource, _cancelTokenSource.Token).Wait();
            Assert.That(doneCalled, Is.True);
        }

        [Test]
        public async Task ErrorHandling_Non206ResponseForRangeRequest_ShouldThrowInvalidOperationException()
        {
            const string url = @"https://mockurl.com/file.png";

            var handler = new MockHttpMessageHandlerCustom((request, ct) =>
            {
                var response = new HttpResponseMessage();
                if (request.Method == HttpMethod.Head)
                {
                    response.StatusCode = System.Net.HttpStatusCode.OK;
                    response.Content = new ByteArrayContent(Array.Empty<byte>());
                    response.Content.Headers.ContentLength = _mockData.Length;
                    response.Headers.AcceptRanges.Add("bytes");
                    return response;
                }

                // Return 200 OK instead of 206 Partial Content for range request
                response.StatusCode = System.Net.HttpStatusCode.OK;
                response.Content = new ByteArrayContent(_mockData);
                return response;
            });

            using var httpClient = new HttpClient(handler);
            var config = new OctaneConfiguration
            {
                Parts = 1,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 1,
                BytesPerSecond = 1
            };

            var client = new OctaneClient(config, httpClient, _factory);

            var ex = Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () =>
            {
                await client.Download(url, (0, _mockData.Length - 1), null, CancellationToken.None, _pauseTokenSource.Token);
            }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.Message, Does.Contain("Expected HTTP 206 Partial Content"));
        }

        [Test]
        public async Task ErrorHandling_InvalidContentRangeHeader_ShouldThrowInvalidOperationException()
        {
            const string url = @"https://mockurl.com/file.png";

            var handler = new MockHttpMessageHandlerCustom((request, ct) =>
            {
                var response = new HttpResponseMessage();
                if (request.Method == HttpMethod.Head)
                {
                    response.StatusCode = System.Net.HttpStatusCode.OK;
                    response.Content = new ByteArrayContent(Array.Empty<byte>());
                    response.Content.Headers.ContentLength = _mockData.Length;
                    response.Headers.AcceptRanges.Add("bytes");
                    return response;
                }

                // Return 206 Partial Content but with mismatched Content-Range
                response.StatusCode = System.Net.HttpStatusCode.PartialContent;
                response.Content = new ByteArrayContent(_mockData);
                // Return range from 10 to 100 when (0, _mockData.Length - 1) was requested
                response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(10, 100, _mockData.Length);
                return response;
            });

            using var httpClient = new HttpClient(handler);
            var config = new OctaneConfiguration
            {
                Parts = 1,
                BufferSize = 8192,
                ShowProgress = false,
                NumRetries = 1,
                BytesPerSecond = 1
            };

            var client = new OctaneClient(config, httpClient, _factory);

            var ex = Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () =>
            {
                await client.Download(url, (0, _mockData.Length - 1), null, CancellationToken.None, _pauseTokenSource.Token);
            }));

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex!.Message, Does.Contain("Invalid or missing Content-Range in response"));
        }
    }

    internal class MockHttpMessageHandlerCustom : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handlerFunc;

        public MockHttpMessageHandlerCustom(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handlerFunc)
        {
            _handlerFunc = handlerFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handlerFunc(request, cancellationToken));
        }
    }
}