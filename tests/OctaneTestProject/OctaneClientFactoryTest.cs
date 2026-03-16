using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using OctaneEngineCore;
using OctaneEngineCore.Clients;
using Serilog;

namespace OctaneTestProject
{
    [TestFixture]
    public class OctaneClientFactoryTest
    {
        private OctaneHTTPClientPool _factory;
        private ILoggerFactory _loggerFactory;

        [SetUp]
        public void Setup()
        {
            // Setup logging using Serilog (matching existing test patterns)
            var seriLog = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Error()
                .WriteTo.Console()
                .CreateLogger();
            _loggerFactory = LoggerFactory.Create(logging =>
            {
                logging.AddSerilog(seriLog);
            });
            
            // Create configuration
            var config = new OctaneConfiguration
            {
                Parts = 4,
                BufferSize = 8192,
                NumRetries = 3,
                RetryCap = 10,
                UseProxy = false
            };
            
            var options = Options.Create(config);
            
            // Create the factory directly
            _factory = new OctaneHTTPClientPool(config, _loggerFactory);
        }

        [TearDown]
        public void Cleanup()
        {
            _factory?.Dispose();
            _loggerFactory?.Dispose();
        }

        [Test]
        public void CreateClient_WithDefaultName_ReturnsHttpClient()
        {
            // Act
            var client = _factory.Rent(null);

            // Assert
            Assert.That(client, Is.Not.Null);
            Assert.That(client, Is.InstanceOf<HttpClient>());
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateClient_WithCustomName_ReturnsHttpClient()
        {
            // Act
            var client = _factory.Rent("test-client");

            // Assert
            Assert.That(client, Is.Not.Null);
            Assert.That(client, Is.InstanceOf<HttpClient>());
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateClient_SameName_ReturnsSameInstance()
        {
            // Act
            var client1 = _factory.Rent("test");
            var client2 = _factory.Rent("test");

            // Assert
            Assert.That(object.ReferenceEquals(client1, client2), Is.True);
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateClient_DifferentNames_ReturnsDifferentInstances()
        {
            // Act
            var client1 = _factory.Rent("client1");
            var client2 = _factory.Rent("client2");

            // Assert
            Assert.That(client1, Is.Not.SameAs(client2));
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(2));
        }

        [Test]
        public void CreateClient_WithConfiguration_AppliesConfiguration()
        {
            // Act
            var client = _factory.Rent("configured", c => 
            {
                c.Timeout = TimeSpan.FromSeconds(60);
                c.DefaultRequestHeaders.Add("X-Custom-Header", "test-value");
            });

            // Assert
            Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(client.DefaultRequestHeaders.Contains("X-Custom-Header"), Is.True);
        }

        [Test]
        public void ClearCache_RemovesAllClients()
        {
            // Arrange
            _factory.Rent("client1");
            _factory.Rent("client2");
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(2));

            // Act
            _factory.Clear();

            // Assert
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_ClearsAllResources()
        {
            // Arrange
            _factory.Rent("client1");
            _factory.Rent("client2");
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(2));

            // Act
            _factory.Dispose();

            // Assert
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(0));
        }

        [Test]
        public async Task CreateClient_CanMakeHttpRequest()
        {
            // Arrange
            var client = _factory.Rent("test");

            // Act & Assert
            var response = await client.GetAsync("https://httpbin.org/get");
            Assert.That(response.IsSuccessStatusCode, Is.True);
        }

        [Test]
        public void CreateClient_WithNullName_UsesDefaultName()
        {
            // Act
            var client1 = _factory.Rent(null);
            var client2 = _factory.Rent(OctaneHTTPClientPool.DEFAULT_CLIENT_NAME);

            // Assert
            Assert.That(client1, Is.SameAs(client2));
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateClient_WithEmptyName_UsesDefaultName()
        {
            // Act
            var client1 = _factory.Rent("");
            var client2 = _factory.Rent(OctaneHTTPClientPool.DEFAULT_CLIENT_NAME);

            // Assert
            Assert.That(client1, Is.SameAs(client2));
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(1));
        }

        [Test]
        public void CreateClient_AfterClearCache_ShouldCreateNewClient()
        {
            // Arrange
            var clientName = "test-client";
            var client1 = _factory.Rent(clientName);
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(1));

            // Act - Clear the cache
            _factory.Clear();
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(0));

            // Act - Create a new client with the same name
            var client2 = _factory.Rent(clientName);

            // Assert
            Assert.That(client2, Is.Not.Null);
            Assert.That(client2, Is.InstanceOf<HttpClient>());
            Assert.That(client2, Is.Not.SameAs(client1)); // Should be a new instance
            Assert.That(_factory.ActiveClientCount, Is.EqualTo(1)); // Should be 1 client in the cache
        }
    }
} 