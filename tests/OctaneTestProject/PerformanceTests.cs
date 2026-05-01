using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OctaneEngineCore.Streams;

namespace OctaneTestProject
{
    [TestFixture]
    public class PerformanceTests
    {
        [Test]
        public async Task ThrottleStream_ReadAsync_EfficiencyTest()
        {
            int numParallel = Math.Max(Environment.ProcessorCount * 4, 32);
            int bps = 1024; // 1KB/s
            int readSize = 1024;
            var tasks = new List<Task>();

            var factory = NullLoggerFactory.Instance;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < numParallel; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var ms = new MemoryStream(new byte[readSize]);
                    var ts = new ThrottleStream(ms, bps, factory);
                    var buffer = new byte[readSize];
                    #if NET6_0_OR_GREATER
                    await ts.ReadAsync(buffer.AsMemory());
                    #else
                    await ts.ReadAsync(buffer, 0, buffer.Length);
                    #endif
                }));
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            Console.WriteLine($"Finished {numParallel} parallel throttled reads in {sw.ElapsedMilliseconds}ms");
            // If it's non-blocking, it should take roughly 1 second (plus some overhead).
            // If it's blocking, it will take (numParallel / NumThreads) * 1 second.
        }
    }
}
