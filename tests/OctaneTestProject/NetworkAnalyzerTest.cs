using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject
{
    [TestFixture]
    public class NetworkAnalyzerTest
    {
        [Test]
        [TestCase(0L, "0 B")]
        [TestCase(1023L, "1023 B")]
        [TestCase(1024L, "1 KB")]
        [TestCase(1536L, "1.5 KB")]
        [TestCase(1048576L, "1 MB")]
        [TestCase(1572864L, "1.5 MB")]
        [TestCase(2147483648L, "2 GB")]
        public void PrettySize_ShouldReturnCorrectFormat(long size, string expected)
        {
            var result = NetworkAnalyzer.PrettySize(size);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
