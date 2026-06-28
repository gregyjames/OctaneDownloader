using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject;

[TestFixture]
public class NetworkAnalyzerTest
{
    [TestCase(500, "500 B")]
    [TestCase(1024, "1 KB")]
    [TestCase(1536, "1.5 KB")]
    [TestCase(1024 * 1024, "1 MB")]
    [TestCase(1572864L, "1.5 MB")]
    [TestCase(1024 * 1024 * 1024, "1 GB")]
    public void PrettySize_ReturnsCorrectFormat(long size, string expected)
    {
        var result = NetworkAnalyzer.PrettySize(size);
        Assert.That(result, Is.EqualTo(expected));
    }
}
