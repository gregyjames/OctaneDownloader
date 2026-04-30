using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using OctaneEngineCore.Streams;

namespace OctaneTestProject
{
    [TestFixture]
    public class ThrottleStreamPerformanceTest
    {
        [Test]
        public async Task ReadAsync_SyncOverAsync_Demonstration()
        {
            var factory = NullLoggerFactory.Instance;
            // Use a number of tasks significantly higher than the number of processors to highlight blocking.
            int numTasks = Environment.ProcessorCount * 8;
            int bps = 1000; // 1KB/s
            var tasks = new List<Task>();

            Console.WriteLine($"Starting {numTasks} parallel throttled reads on {Environment.ProcessorCount} cores...");
            var startTime = DateTime.UtcNow;

            for (int i = 0; i < numTasks; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var data = new byte[2000];
                    using var ms = new MemoryStream(data);
                    var ts = new ThrottleStream(ms, bps, factory);
                    var buffer = new byte[100];

                    // Read 5 times, 100 bytes each. Total 500 bytes.
                    // At 1000 BPS, this should take at least 0.5 seconds of total "sleep" time.
                    for(int j=0; j<5; j++)
                    {
                        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
                            await ts.ReadAsync(new Memory<byte>(buffer));
                        #else
                            await ts.ReadAsync(buffer, 0, 100);
                        #endif
                    }
                }));
            }

            await Task.WhenAll(tasks);
            var duration = DateTime.UtcNow - startTime;

            Console.WriteLine($"Completed {numTasks} throttled reads in {duration.TotalMilliseconds}ms");

            // In a perfectly non-blocking world, with enough BPS per stream, it should take ~0.5s.
            // If it blocks threads, it might take much longer depending on thread pool growth.
        }
    }
}
