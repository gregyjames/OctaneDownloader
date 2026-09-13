using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject;

[TestFixture]
public class NetworkAnalyzerTest
{
    [TestCase(0L, "0 B")]
    [TestCase(1L, "1 B")]
    [TestCase(500L, "500 B")]
    [TestCase(1023L, "1023 B")]
    [TestCase(1024L, "1 KB")]
    [TestCase(1500L, "1.46 KB")]
    [TestCase(1020000L, "996.09 KB")]
    [TestCase(1048576L, "1 MB")]
    [TestCase(1500000L, "1.43 MB")]
    [TestCase(1572864L, "1.5 MB")]
    [TestCase(1073741824L, "1 GB")]
    [TestCase(1610612736L, "1.5 GB")]
    [TestCase(1099511627776L, "1 TB")]
    [TestCase(1649267441664L, "1.5 TB")]
    public void PrettySize_FormatsCorrectly(long inputBytes, string expected)
    {
        var result = NetworkAnalyzer.PrettySize(inputBytes);
        Assert.That(result, Is.EqualTo(expected));
    }
}