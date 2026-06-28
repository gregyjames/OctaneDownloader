using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject
{
    [TestFixture]
    public class NetworkAnalyzerTest
    {
        [Test]
        [TestCase(500, "500 B")]
        [TestCase(1024, "1 KB")]
        [TestCase(1500, "1.46 KB")]
        [TestCase(1024 * 1024, "1 MB")]
        [TestCase(1572864, "1.5 MB")]
        [TestCase(2147483648L, "2 GB")]
        public void PrettySize_ShouldReturnCorrectFormat(long size, string expected)
        {
            var result = NetworkAnalyzer.PrettySize(size);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
