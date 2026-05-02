using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using OctaneEngineCore.Streams;
using Serilog;

namespace OctaneTestProject
{
    [TestFixture]
    public class ThrottleStreamTest
    {
        private ILoggerFactory _factory;

        [SetUp]
        public void Setup()
        {
            var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger();
            _factory = LoggerFactory.Create(logging => logging.AddSerilog(logger));
        }

        [TearDown]
        public void Teardown()
        {
            _factory?.Dispose();
        }

        [Test]
        public void Constructor_Default_ShouldNotThrow()
        {
            Assert.DoesNotThrow(() => new ThrottleStream(_factory));
        }

        [Test]
        public void Constructor_WithStream_ShouldWrapStream()
        {
            using var ms = new MemoryStream();
            var ts = new ThrottleStream(ms, 1024, _factory);
            Assert.That(ts.CanRead, Is.True);
            Assert.That(ts.CanWrite, Is.True);
            Assert.That(ts.CanSeek, Is.True);
        }

        [Test]
        public void SetStreamParent_ShouldReplaceStream()
        {
            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();
            var ts = new ThrottleStream(ms1, 1024, _factory);
            ts.SetStreamParent(ms2);
            Assert.That(ts.CanRead, Is.True);
        }

        [Test]
        public void SetBps_ShouldUpdateMaxBps()
        {
            using var ms = new MemoryStream();
            var ts = new ThrottleStream(ms, 1024, _factory);
            ts.SetBps(2048);
            // No direct way to check, but should not throw
            Assert.Pass();
        }

        [Test]
        public void WriteAndRead_ShouldThrottleAndWorkCorrectly()
        {
            using var ms = new MemoryStream();
            var ts = new ThrottleStream(ms, 1024 * 1024, _factory); // High BPS to avoid actual throttling delay
            var data = new byte[] {1, 2, 3, 4, 5};
            ts.Write(data, 0, data.Length);
            ts.Flush();
            ts.Position = 0;
            var buffer = new byte[5];
            var bytesRead = ts.Read(buffer, 0, 5);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(buffer, Is.EqualTo(data));
        }

        [Test]
        public void Read_PastEndOfStream_ShouldReturnBytesRead()
        {
            using var ms = new MemoryStream(new byte[] {1, 2, 3});
            var ts = new ThrottleStream(ms, 1024 * 1024, _factory);
            var buffer = new byte[10];
            var bytesRead = ts.Read(buffer, 0, 10);
            Assert.That(bytesRead, Is.EqualTo(3));
        }

        [Test]
        public void SeekAndSetLength_ShouldWork()
        {
            using var ms = new MemoryStream();
            var ts = new ThrottleStream(ms, 1024, _factory);
            ts.SetLength(100);
            Assert.That(ts.Length, Is.EqualTo(100));
            ts.Seek(10, SeekOrigin.Begin);
            Assert.That(ts.Position, Is.EqualTo(10));
        }

        [Test]
        public async Task DisposeAsync_ShouldDisposeStream()
        {
            var ms = new MemoryStream();
            var ts = new ThrottleStream(ms, 1024, _factory);
            await ts.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => ms.WriteByte(1));
        }

        [Test]
        public void Dispose_ShouldDisposeStream()
        {
            var ms = new MemoryStream();
            var ts = new ThrottleStream(ms, 1024, _factory);
            ts.Dispose();
            Assert.Throws<ObjectDisposedException>(() => ms.WriteByte(1));
        }

        [Test]
        public async Task ReadAsync_ShouldThrottleWithoutBlocking()
        {
            // Set up a stream with enough data
            var data = new byte[100];
            new Random().NextBytes(data);
            using var ms = new MemoryStream(data);

            // Throttle to 10 bytes per second (so 100 bytes should take 10 seconds)
            var ts = new ThrottleStream(ms, 10, _factory);
            var buffer = new byte[100].AsMemory();

            var sw = Stopwatch.StartNew();
            // Start the read task using the Memory overload to ensure we hit the async path
            Console.WriteLine($"Starting ReadAsync at {sw.ElapsedMilliseconds}ms");
            var readTask = ts.ReadAsync(buffer).AsTask();
            var callElapsed = sw.ElapsedMilliseconds;
            Console.WriteLine($"ReadAsync call returned in {callElapsed}ms. Task status: {readTask.Status}");

            // If it's blocking, callElapsed will be large (~10s) because MemoryStream.ReadAsync completes synchronously,
            // and then Throttle() is called which blocks.

            // We want it to be truly async.
            Assert.That(callElapsed, Is.LessThan(1000), "ReadAsync call should return quickly even if throttled");
            Assert.That(readTask.IsCompleted, Is.False, "ReadTask should be pending due to throttling");

            await readTask;
            Console.WriteLine($"ReadTask completed at {sw.ElapsedMilliseconds}ms");
        }
    }
}