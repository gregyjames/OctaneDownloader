using NUnit.Framework;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneTestProject
{
    [TestFixture]
    public class NetworkAnalyzerTest
    {
        [TestCase(0L, "0 B")]
        [TestCase(1L, "1 B")]
        [TestCase(512L, "512 B")]
        [TestCase(1023L, "1023 B")]
        [TestCase(1024L, "1 KB")]
        [TestCase(1500L, "1.46 KB")]
        [TestCase(2048L, "2 KB")]
        [TestCase(1048576L, "1 MB")]
        [TestCase(1572864L, "1.5 MB")]
        [TestCase(1073741824L, "1 GB")]
        [TestCase(1610612736L, "1.5 GB")]
        [TestCase(1099511627776L, "1 TB")]
        [TestCase(1649267441664L, "1.5 TB")]
        public void PrettySize_ShouldReturnExpectedString(long bytes, string expected)
        {
            var result = NetworkAnalyzer.PrettySize(bytes);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void PrettySize_Generate55TestCases()
        {
            // We will verify 55 distinct inputs to comprehensively cover B, KB, MB, GB, TB
            // and ensure double precision formatting is absolutely flawless.

            // 1. Bytes range (11 cases: 0 to 10 bytes)
            for (long i = 0; i <= 10; i++)
            {
                Assert.That(NetworkAnalyzer.PrettySize(i), Is.EqualTo($"{i} B"));
            }

            // 2. Kilobytes range (11 cases: 1024 to 1024 + 10*100 bytes)
            for (int i = 0; i < 11; i++)
            {
                long bytes = 1024L + i * 100L;
                double expectedValue = bytes / 1024.0;
                string expectedStr = $"{expectedValue:0.##} KB";
                Assert.That(NetworkAnalyzer.PrettySize(bytes), Is.EqualTo(expectedStr));
            }

            // 3. Megabytes range (11 cases: 1024*1024 to 1024*1024 + 10*100000 bytes)
            for (int i = 0; i < 11; i++)
            {
                long bytes = 1048576L + i * 100000L;
                double expectedValue = bytes / 1048576.0;
                string expectedStr = $"{expectedValue:0.##} MB";
                Assert.That(NetworkAnalyzer.PrettySize(bytes), Is.EqualTo(expectedStr));
            }

            // 4. Gigabytes range (11 cases: 1024*1024*1024 to 1024*1024*1024 + 10*100000000 bytes)
            for (int i = 0; i < 11; i++)
            {
                long bytes = 1073741824L + i * 100000000L;
                double expectedValue = bytes / 1073741824.0;
                string expectedStr = $"{expectedValue:0.##} GB";
                Assert.That(NetworkAnalyzer.PrettySize(bytes), Is.EqualTo(expectedStr));
            }

            // 5. Terabytes range (11 cases: 1024*1024*1024*1024 to 1024*1024*1024*1024 + 10*100000000000 bytes)
            for (int i = 0; i < 11; i++)
            {
                long bytes = 1099511627776L + i * 100000000000L;
                double expectedValue = bytes / 1099511627776.0;
                string expectedStr = $"{expectedValue:0.##} TB";
                Assert.That(NetworkAnalyzer.PrettySize(bytes), Is.EqualTo(expectedStr));
            }
        }
    }
}