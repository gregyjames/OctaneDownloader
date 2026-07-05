using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject;

[TestFixture]
public class NetworkAnalyzerTest
{
    [TestCase(500, "500 B")]
    [TestCase(1024, "1 KB")]
    [TestCase(1500, "1.46 KB")]
    [TestCase(1024 * 1024, "1 MB")]
    [TestCase(1024 * 1024 * 1024, "1 GB")]
    [TestCase(1500000, "1.43 MB")]
    public void PrettySize_CorrectlyFormatsSizes(long size, string expected)
    {
        var result = NetworkAnalyzer.PrettySize(size);
        Assert.That(result, Is.EqualTo(expected));
    }
}
