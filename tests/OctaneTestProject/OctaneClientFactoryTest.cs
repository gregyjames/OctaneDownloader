using System;
using System.Net.Http;
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
    }
}