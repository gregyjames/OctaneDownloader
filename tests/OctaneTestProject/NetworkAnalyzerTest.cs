using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject;

[TestFixture]
public class NetworkAnalyzerTest
{
    [Test]
    [TestCase(0, "0 B")]
    [TestCase(1023, "1023 B")]
    [TestCase(1024, "1 KB")]
    [TestCase(1500, "1.46 KB")]
    [TestCase(1048576, "1 MB")]
    [TestCase(1572864, "1.5 MB")]
    [TestCase(1073741824, "1 GB")]
    [TestCase(1610612736, "1.5 GB")]
    public void PrettySize_ReturnsCorrectFormat(long bytes, string expected)
    {
        var result = NetworkAnalyzer.PrettySize(bytes);
        Assert.That(result, Is.EqualTo(expected));
    }
}
