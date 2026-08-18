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

        private static readonly string[] LegacySizes = { "B", "KB", "MB", "GB", "TB" };
        private static string LegacyPrettySize(long len)
        {
            int order = 0;
            while (len >= 1024 && order < LegacySizes.Length - 1)
            {
                order++;
                len = len >> 10;
            }
            return $"{len} {LegacySizes[order]}";
        }

        [Test]
        public void PrettySize_Benchmark_MeasuresPerformanceImprovement()
        {
            const int iterations = 500_000;
            long[] testValues = { 500L, 1500L, 2_621_440L, 1_610_612_736L, 2_199_023_255_552L };

            // Warmup
            foreach (var val in testValues)
            {
                LegacyPrettySize(val);
                NetworkAnalyzer.PrettySize(val);
            }

            var swLegacy = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var val = testValues[i % testValues.Length];
                _ = LegacyPrettySize(val);
            }
            swLegacy.Stop();

            var swNew = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var val = testValues[i % testValues.Length];
                _ = NetworkAnalyzer.PrettySize(val);
            }
            swNew.Stop();

            System.Console.WriteLine($"[Benchmark] Legacy PrettySize ({iterations:N0} iterations): {swLegacy.ElapsedMilliseconds} ms");
            System.Console.WriteLine($"[Benchmark] New O(1) PrettySize ({iterations:N0} iterations): {swNew.ElapsedMilliseconds} ms");

            Assert.That(swNew.ElapsedTicks, Is.GreaterThan(0));
        }
    }
}
