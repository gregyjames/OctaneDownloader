using System;
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
        [TestCase(1048576, ExpectedResult = "1 MB")]
        [TestCase(1572864, ExpectedResult = "1.5 MB")]
        [TestCase(1073741824, ExpectedResult = "1 GB")]
        [TestCase(1610612736, ExpectedResult = "1.5 GB")]
        [TestCase(1099511627776, ExpectedResult = "1 TB")]
        [TestCase(1649267441664, ExpectedResult = "1.5 TB")]
        [TestCase(-1, ExpectedResult = "-1 B")]
        [TestCase(-1024, ExpectedResult = "-1024 B")]
        public string TestPrettySize(long size)
        {
            return NetworkAnalyzer.PrettySize(size);
        }

        [Test]
        public void BenchmarkPrettySize()
        {
            long[] testSizes = { 0, 500, 1023, 1024, 1500, 1048576, 1572864, 1073741824, 1610612736, 1099511627776, 1649267441664, -1, -1024 };

            // Warm up
            for (int i = 0; i < 10000; i++)
            {
                foreach (var size in testSizes)
                {
                    _ = NetworkAnalyzer.PrettySize(size);
                }
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int iterations = 1000000;
            for (int i = 0; i < iterations; i++)
            {
                foreach (var size in testSizes)
                {
                    _ = NetworkAnalyzer.PrettySize(size);
                }
            }
            sw.Stop();

            Console.WriteLine($"###BENCHMARK### Baseline pretty-print took {sw.ElapsedMilliseconds} ms for {iterations * testSizes.Length} iterations.");
        }
    }
}
