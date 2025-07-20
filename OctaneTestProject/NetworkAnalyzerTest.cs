using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using OctaneEngineCore;
using OctaneEngineCore.Implementations;
using OctaneEngineCore.Interfaces;

namespace OctaneTestProject
{
    [TestFixture]
    public class NetworkAnalyzerTest
    {
        [TestCase(0, ExpectedResult = "0 B")]
        [TestCase(512, ExpectedResult = "512 B")]
        [TestCase(1024, ExpectedResult = "1 KB")]
        [TestCase(1048576, ExpectedResult = "1 MB")]
        [TestCase(1073741824, ExpectedResult = "1 GB")]
        public string PrettySize_ShouldFormatCorrectly(long input)
        {
            return NetworkAnalyzer.PrettySize(input);
        }

        [Test]
        public void GetTestFile_ShouldReturnCorrectUrlAndSize()
        {
            var (urlSmall, sizeSmall) = NetworkAnalyzer.GetTestFile(TestFileSize.Small);
            Assert.That(urlSmall, Does.Contain("1MB"));
            Assert.That(sizeSmall, Is.EqualTo(1000000));

            var (urlMedium, sizeMedium) = NetworkAnalyzer.GetTestFile(TestFileSize.Medium);
            Assert.That(urlMedium, Does.Contain("7MB"));
            Assert.That(sizeMedium, Is.EqualTo(7000000));

            var (urlLarge, sizeLarge) = NetworkAnalyzer.GetTestFile(TestFileSize.Large);
            Assert.That(urlLarge, Does.Contain("15MB"));
            Assert.That(sizeLarge, Is.EqualTo(15000000));
        }

        [Test]
        public async Task GetCurrentNetworkLatency_ShouldReturnStringWithMs()
        {
            var mockPingService = new Mock<IPingService>();
            mockPingService.Setup(p => p.SendPingAsync(It.IsAny<string>()))
                .ReturnsAsync(new PingReplyMock(IPStatus.Success, 42)); // You may need to create a PingReply mock

            var result = await NetworkAnalyzer.GetCurrentNetworkLatency(mockPingService.Object);
            Assert.That(result, Does.EndWith("ms"));
        }

        [Test]
        public async Task GetCurrentNetworkSpeed_ShouldReturnStringWithMbPerSec()
        {
            var mockDownloader = new Mock<IHttpDownloader>();
            mockDownloader.Setup(d => d.GetByteArrayAsync(It.IsAny<string>()))
                .ReturnsAsync(new byte[100]); // Simulate a 100-byte download
            
            // This test will actually download a file. Consider skipping in CI or mocking if needed.
            var result = await NetworkAnalyzer.GetCurrentNetworkSpeed(mockDownloader.Object);
            Assert.That(result, Does.Contain("Mb/s").Or.Contain("GB/s"));
        }
    }
} 