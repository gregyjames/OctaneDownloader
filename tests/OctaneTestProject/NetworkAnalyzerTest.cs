using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject
{
    [TestFixture]
    public class NetworkAnalyzerTest
    {
        [TestCase(0, "0 B")]
        [TestCase(1, "1 B")]
        [TestCase(500, "500 B")]
        [TestCase(1023, "1023 B")]
        [TestCase(1024, "1 KB")]
        [TestCase(1500, "1.46 KB")]
        [TestCase(1048576, "1 MB")]
        [TestCase(2621440, "2.5 MB")]
        [TestCase(1073741824, "1 GB")]
        [TestCase(1610612736, "1.5 GB")]
        [TestCase(1099511627776L, "1 TB")]
        [TestCase(2199023255552L, "2 TB")]
        [TestCase(-5, "0 B")]
        public void PrettySize_FormatsCorrectly(long bytes, string expected)
        {
            var actual = NetworkAnalyzer.PrettySize(bytes);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void PrettySize_LargeRandomRange_DoesNotThrowAndMaintainsFormat()
        {
            for (int i = 0; i < 1000; i++)
            {
                long bytes = i * 1234567L;
                var actual = NetworkAnalyzer.PrettySize(bytes);
                Assert.That(actual, Is.Not.Null);
                Assert.That(actual, Does.Contain(" ") & (Does.Contain("B") | Does.Contain("KB") | Does.Contain("MB") | Does.Contain("GB") | Does.Contain("TB")));
            }
        }
    }
}
