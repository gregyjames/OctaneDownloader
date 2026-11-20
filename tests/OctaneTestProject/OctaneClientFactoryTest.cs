using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using OctaneEngineCore;
using OctaneEngineCore.Clients;

namespace OctaneTestProject
{
    [TestFixture]
    public class OctaneClientFactoryTest
    {
        private IServiceProvider _serviceProvider;

        [SetUp]
        public void Setup()
        {
            var services = new ServiceCollection();
            
            // Create configuration
            var config = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 8192,
                NumRetries = 3,
                RetryCap = 10,
                UseProxy = false
            };
            
            services.AddSingleton(Options.Create(config));
            services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(builder => { }));
            services.AddClient();
            
            _serviceProvider = services.BuildServiceProvider();
        }

        [Test]
        public void CreateClient_ResolvesFromHttpClientFactory()
        {
            // Arrange
            var factory = _serviceProvider.GetRequiredService<IHttpClientFactory>();

            // Act
            var client = factory.CreateClient("OctaneClient");

            // Assert
            Assert.That(client, Is.Not.Null);
            Assert.That(client, Is.InstanceOf<HttpClient>());
            Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromMinutes(30)));
            Assert.That(client.DefaultRequestHeaders.Contains("User-Agent"), Is.True);
            Assert.That(client.DefaultRequestHeaders.Contains("Accept-Encoding"), Is.False);
            Assert.That(client.DefaultRequestVersion, Is.EqualTo(System.Net.HttpVersion.Version11));
            Assert.That(client.DefaultVersionPolicy, Is.EqualTo(HttpVersionPolicy.RequestVersionOrLower));
        }

        [Test]
        public void CreateClient_UsesConfiguredHttpVersionAndPolicy()
        {
            var services = new ServiceCollection();
            var config = new OctaneConfiguration
            {
                HttpVersion = System.Net.HttpVersion.Version20,
                HttpVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };

            services.AddSingleton(Options.Create(config));
            services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(builder => { }));
            services.AddClient();

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("OctaneClient");

            Assert.That(client.DefaultRequestVersion, Is.EqualTo(System.Net.HttpVersion.Version20));
            Assert.That(client.DefaultVersionPolicy, Is.EqualTo(HttpVersionPolicy.RequestVersionOrHigher));
        }

        [Test]
        public void SingleHttpClientFactory_ReturnsCorrectClient()
        {
            // Arrange
            using var mockClient = new HttpClient();
            var factory = new SingleHttpClientFactory(mockClient);

            // Act
            var client = factory.CreateClient("AnyName");

            // Assert
            Assert.That(client, Is.SameAs(mockClient));
        }

        [Test]
        public async Task AddClient_RemovesDefaultHttpClientLoggers()
        {
            var loggerProvider = new RecordingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Trace));

            var services = new ServiceCollection();
            services.AddSingleton(Options.Create(new OctaneConfiguration { NumRetries = 0, UseProxy = false }));
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddClient();

            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("OctaneClient");

            using var server = new LoopbackHttpServer(_ => (HttpStatusCode.OK, "ok"));
            using var response = await client.GetAsync(server.BaseUri);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(loggerProvider.Categories, Has.None.StartsWith("System.Net.Http.HttpClient"));
        }

        [Test]
        public async Task RetryHandler_ZeroRetries_SendsSingleRequestAndReturnsResponse()
        {
            var client = CreateConfiguredClient(numRetries: 0, retryCap: 0, out var provider);
            using var _ = provider;
            using var server = new LoopbackHttpServer(_ => (HttpStatusCode.ServiceUnavailable, "busy"));

            using var response = await client.GetAsync(server.BaseUri);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(server.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RetryHandler_NonTransient404_ReturnsImmediatelyWithoutRetrying()
        {
            var client = CreateConfiguredClient(numRetries: 3, retryCap: 0, out var provider);
            using var _ = provider;
            using var server = new LoopbackHttpServer(_ => (HttpStatusCode.NotFound, "missing"));

            using var response = await client.GetAsync(server.BaseUri);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(server.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RetryHandler_TransientThenSuccess_RetriesAndReturnsSuccess()
        {
            var client = CreateConfiguredClient(numRetries: 3, retryCap: 0, out var provider);
            using var _ = provider;
            using var server = new LoopbackHttpServer(attempt =>
                attempt == 1
                    ? (HttpStatusCode.ServiceUnavailable, "busy")
                    : (HttpStatusCode.OK, "ok"));

            using var response = await client.GetAsync(server.BaseUri);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(server.RequestCount, Is.EqualTo(2));
        }

        [Test]
        public async Task RetryHandler_ExhaustedTransientRetries_SkipsTrailingDelayAndReturnsLastResponse()
        {
            // RetryCap = 10 would add a 2s trailing delay on attempt 2 if trailing delay weren't skipped.
            // With numRetries = 2 and retryCap = 10: attempt 1 delays 1s, attempt 2 must NOT delay 2s.
            var client = CreateConfiguredClient(numRetries: 2, retryCap: 10, out var provider);
            using var _ = provider;
            using var server = new LoopbackHttpServer(_ => (HttpStatusCode.ServiceUnavailable, "busy"));

            var sw = Stopwatch.StartNew();
            using var response = await client.GetAsync(server.BaseUri);
            sw.Stop();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(server.RequestCount, Is.EqualTo(2));
            Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2.5)));
        }

        [Test]
        public void EngineBuilder_WithHttpClient_DoesNotBuildUnusedServiceProvider()
        {
            var loggerProvider = new RecordingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Trace));
            using var customClient = new HttpClient();

            using var engine = (IDisposable)EngineBuilder.Create()
                .WithLogger(loggerFactory)
                .WithHttpClient(customClient)
                .Build();

            Assert.That(engine, Is.Not.Null);
            // RetryHandler logs on construction inside AddClient(); if no ServiceProvider was built, it was never created.
            Assert.That(loggerProvider.Categories, Has.None.Contains("RetryHandler"));
        }

        private static HttpClient CreateConfiguredClient(int numRetries, int retryCap, out ServiceProvider provider)
        {
            var services = new ServiceCollection();
            services.AddSingleton(Options.Create(new OctaneConfiguration
            {
                NumRetries = numRetries,
                RetryCap = retryCap,
                UseProxy = false
            }));
            services.AddSingleton<ILoggerFactory>(LoggerFactory.Create(_ => { }));
            services.AddClient();

            provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IHttpClientFactory>().CreateClient("OctaneClient");
        }

        private sealed class RecordingLoggerProvider : ILoggerProvider
        {
            public ConcurrentBag<string> Categories { get; } = new();

            public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Categories);

            public void Dispose() { }

            private sealed class RecordingLogger(string category, ConcurrentBag<string> categories) : ILogger
            {
                public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
                public bool IsEnabled(LogLevel logLevel) => true;
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    categories.Add(category);
                }

                private sealed class NullScope : IDisposable
                {
                    public static readonly NullScope Instance = new();
                    public void Dispose() { }
                }
            }
        }

        private sealed class LoopbackHttpServer : IDisposable
        {
            private readonly TcpListener _listener;
            private readonly CancellationTokenSource _cts = new();
            private readonly Func<int, (HttpStatusCode Status, string Body)> _responder;
            private readonly Task _acceptLoop;
            private int _requestCount;

            public int RequestCount => Volatile.Read(ref _requestCount);
            public Uri BaseUri { get; }

            public LoopbackHttpServer(Func<int, (HttpStatusCode Status, string Body)> responder)
            {
                _responder = responder;
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                BaseUri = new Uri($"http://127.0.0.1:{port}/");
                _acceptLoop = Task.Run(AcceptLoopAsync);
            }

            private async Task AcceptLoopAsync()
            {
                while (!_cts.IsCancellationRequested)
                {
                    TcpClient client;
                    try
                    {
                        client = await _listener.AcceptTcpClientAsync(_cts.Token);
                    }
                    catch
                    {
                        break;
                    }

                    _ = Task.Run(() => HandleClientAsync(client));
                }
            }

            private async Task HandleClientAsync(TcpClient client)
            {
                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true))
                {
                    while (!_cts.IsCancellationRequested)
                    {
                        var requestLine = await reader.ReadLineAsync(_cts.Token);
                        if (string.IsNullOrEmpty(requestLine))
                            break;

                        while (true)
                        {
                            var headerLine = await reader.ReadLineAsync(_cts.Token);
                            if (string.IsNullOrEmpty(headerLine))
                                break;
                        }

                        var count = Interlocked.Increment(ref _requestCount);
                        var (status, body) = _responder(count);
                        var bodyBytes = Encoding.UTF8.GetBytes(body);
                        var responseHeaders = $"HTTP/1.1 {(int)status} {status}\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                        var headerBytes = Encoding.ASCII.GetBytes(responseHeaders);
                        await stream.WriteAsync(headerBytes, _cts.Token);
                        await stream.WriteAsync(bodyBytes, _cts.Token);
                        await stream.FlushAsync(_cts.Token);
                        break;
                    }
                }
            }

            public void Dispose()
            {
                _cts.Cancel();
                _listener.Stop();
                try { _acceptLoop.Wait(TimeSpan.FromSeconds(1)); } catch { }
                _cts.Dispose();
            }
        }
    }
}