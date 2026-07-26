using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject
{
    [TestFixture]
    public class NetworkAnalyzerTest
    {
        [TestCase(0, ExpectedResult = "0 B")]
        [TestCase(500, ExpectedResult = "500 B")]
        [TestCase(1023, ExpectedResult = "1023 B")]
        [TestCase(1024, ExpectedResult = "1 KB")]
        [TestCase(1500, ExpectedResult = "1.46 KB")]
        [TestCase(1048575, ExpectedResult = "1024 KB")]
        [TestCase(1048576, ExpectedResult = "1 MB")]
        [TestCase(1572864, ExpectedResult = "1.5 MB")]
        [TestCase(1073741824, ExpectedResult = "1 GB")]
        [TestCase(1099511627776L, ExpectedResult = "1 TB")]
        public string TestPrettySize(long bytes)
        {
            return NetworkAnalyzer.PrettySize(bytes);
        }
    }
}
