using System;
using System.Threading.Tasks;
using NUnit.Framework;
using OctaneEngineCore;

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
            var (urlSmall, sizeSmall) = NetworkAnalyzer.GetTestFile(NetworkAnalyzer.TestFileSize.Small);
            Assert.That(urlSmall, Does.Contain("1MB"));
            Assert.That(sizeSmall, Is.EqualTo(1000000));

            var (urlMedium, sizeMedium) = NetworkAnalyzer.GetTestFile(NetworkAnalyzer.TestFileSize.Medium);
            Assert.That(urlMedium, Does.Contain("7MB"));
            Assert.That(sizeMedium, Is.EqualTo(7000000));

            var (urlLarge, sizeLarge) = NetworkAnalyzer.GetTestFile(NetworkAnalyzer.TestFileSize.Large);
            Assert.That(urlLarge, Does.Contain("15MB"));
            Assert.That(sizeLarge, Is.EqualTo(15000000));
        }

        [Test]
        [Ignore("Does not work properly on CI.")]
        public async Task GetCurrentNetworkLatency_ShouldReturnStringWithMs()
        {
            var result = await NetworkAnalyzer.GetCurrentNetworkLatency();
            Assert.That(result, Does.EndWith("ms"));
        }

        [Test]
        [Ignore("Does not work properly on CI.")]
        public async Task GetCurrentNetworkSpeed_ShouldReturnStringWithMbPerSec()
        {
            // This test will actually download a file. Consider skipping in CI or mocking if needed.
            var result = await NetworkAnalyzer.GetCurrentNetworkSpeed();
            Assert.That(result, Does.Contain("Mb/s").Or.Contain("GB/s"));
        }
    }
} 