using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject
{
    [TestFixture]
    public class NetworkAnalyzerTest
    {
        [TestCase(0L, "0 B")]
        [TestCase(-10L, "0 B")]
        [TestCase(1L, "1 B")]
        [TestCase(100L, "100 B")]
        [TestCase(512L, "512 B")]
        [TestCase(1023L, "1023 B")]

        [TestCase(1024L, "1 KB")]
        [TestCase(1500L, "1.46 KB")]
        [TestCase(2048L, "2 KB")]
        [TestCase(10240L, "10 KB")]
        [TestCase(102400L, "100 KB")]
        [TestCase(524288L, "512 KB")]
        [TestCase(1047552L, "1023 KB")]

        [TestCase(1048576L, "1 MB")]
        [TestCase(1572864L, "1.5 MB")]
        [TestCase(2097152L, "2 MB")]
        [TestCase(10485760L, "10 MB")]
        [TestCase(104857600L, "100 MB")]
        [TestCase(536870912L, "512 MB")]
        [TestCase(1072693248L, "1023 MB")]

        [TestCase(1073741824L, "1 GB")]
        [TestCase(1610612736L, "1.5 GB")]
        [TestCase(2147483648L, "2 GB")]
        [TestCase(10737418240L, "10 GB")]
        [TestCase(107374182400L, "100 GB")]
        [TestCase(549755813888L, "512 GB")]
        [TestCase(1098437885952L, "1023 GB")]

        [TestCase(1099511627776L, "1 TB")]
        [TestCase(1649267441664L, "1.5 TB")]
        [TestCase(2199023255552L, "2 TB")]
        [TestCase(10995116277760L, "10 TB")]
        [TestCase(109951162777600L, "100 TB")]
        [TestCase(562949953421312L, "512 TB")]
        [TestCase(1099511627776000L, "1000 TB")]
        public void PrettySize_VariousValues_ShouldMatchExpected(long input, string expected)
        {
            var result = NetworkAnalyzer.PrettySize(input);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void PrettySize_DecimalPrecision_ShouldBePreserved()
        {
            // Verify up to two decimal places
            Assert.That(NetworkAnalyzer.PrettySize(1024L + 512L), Is.EqualTo("1.5 KB"));
            Assert.That(NetworkAnalyzer.PrettySize(1024L + 256L), Is.EqualTo("1.25 KB"));
            Assert.That(NetworkAnalyzer.PrettySize(1024L + 128L), Is.EqualTo("1.13 KB")); // 1.125 rounded up/even
            Assert.That(NetworkAnalyzer.PrettySize(1024L + 64L), Is.EqualTo("1.06 KB"));
            Assert.That(NetworkAnalyzer.PrettySize(1024L + 32L), Is.EqualTo("1.03 KB"));
            Assert.That(NetworkAnalyzer.PrettySize(1024L + 16L), Is.EqualTo("1.02 KB"));
            Assert.That(NetworkAnalyzer.PrettySize(1024L + 8L), Is.EqualTo("1.01 KB"));
            Assert.That(NetworkAnalyzer.PrettySize(1024L + 4L), Is.EqualTo("1 KB")); // 1.0039 -> 1
            Assert.That(NetworkAnalyzer.PrettySize(1048576L + 524288L), Is.EqualTo("1.5 MB"));
            Assert.That(NetworkAnalyzer.PrettySize(1048576L + 262144L), Is.EqualTo("1.25 MB"));
            Assert.That(NetworkAnalyzer.PrettySize(1073741824L + 536870912L), Is.EqualTo("1.5 GB"));
            Assert.That(NetworkAnalyzer.PrettySize(1073741824L + 268435456L), Is.EqualTo("1.25 GB"));
            Assert.That(NetworkAnalyzer.PrettySize(1099511627776L + 549755813888L), Is.EqualTo("1.5 TB"));
            Assert.That(NetworkAnalyzer.PrettySize(1099511627776L + 274877906944L), Is.EqualTo("1.25 TB"));

            // Check some arbitrary precise values
            Assert.That(NetworkAnalyzer.PrettySize(1234567L), Is.EqualTo("1.18 MB"));
            Assert.That(NetworkAnalyzer.PrettySize(987654321L), Is.EqualTo("941.9 MB"));
            Assert.That(NetworkAnalyzer.PrettySize(123456789012L), Is.EqualTo("114.98 GB"));
            Assert.That(NetworkAnalyzer.PrettySize(9876543210123L), Is.EqualTo("8.98 TB"));
            Assert.That(NetworkAnalyzer.PrettySize(999999999999999L), Is.EqualTo("909.49 TB"));
        }
    }
}