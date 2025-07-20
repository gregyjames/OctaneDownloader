using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OctaneEngineCore.Streams;

namespace OctaneTestProject
{
    [TestFixture]
    public class NormalStreamTest
    {
        [Test]
        public void Constructor_Default_ShouldSetStreamToNull()
        {
            var ns = new NormalStream();
            Assert.Throws<NullReferenceException>(() => { var _ = ns.CanRead; });
        }

        [Test]
        public void Constructor_WithStream_ShouldWrapStream()
        {
            using var ms = new MemoryStream();
            var ns = new NormalStream(ms);
            Assert.That(ns.CanRead, Is.True);
            Assert.That(ns.CanWrite, Is.True);
            Assert.That(ns.CanSeek, Is.True);
        }

        [Test]
        public void SetStreamParent_ShouldReplaceStream()
        {
            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();
            var ns = new NormalStream(ms1);
            ns.SetStreamParent(ms2);
            Assert.That(ns.CanRead, Is.True);
        }

        [Test]
        public void WriteAndRead_ShouldWorkCorrectly()
        {
            using var ms = new MemoryStream();
            var ns = new NormalStream(ms);
            var data = new byte[] {1, 2, 3, 4, 5};
            ns.Write(data, 0, data.Length);
            ns.Flush();
            ns.Position = 0;
            var buffer = new byte[5];
            var bytesRead = ns.Read(buffer, 0, 5);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(buffer, Is.EqualTo(data));
        }

        [Test]
        public void Read_PastEndOfStream_ShouldReturnBytesRead()
        {
            using var ms = new MemoryStream(new byte[] {1, 2, 3});
            var ns = new NormalStream(ms);
            var buffer = new byte[10];
            var bytesRead = ns.Read(buffer, 0, 10);
            Assert.That(bytesRead, Is.EqualTo(3));
        }

        [Test]
        public void SeekAndSetLength_ShouldWork()
        {
            using var ms = new MemoryStream();
            var ns = new NormalStream(ms);
            ns.SetLength(100);
            Assert.That(ns.Length, Is.EqualTo(100));
            ns.Seek(10, SeekOrigin.Begin);
            Assert.That(ns.Position, Is.EqualTo(10));
        }

        [Test]
        public async Task ReadAsync_ShouldReadCorrectly()
        {
            using var ms = new MemoryStream(new byte[] {1, 2, 3, 4, 5});
            var ns = new NormalStream(ms);
            var buffer = new byte[5];
            var bytesRead = await ns.ReadAsync(buffer, 0, 5, CancellationToken.None);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(buffer, Is.EqualTo(new byte[] {1, 2, 3, 4, 5}));
        }

        [Test]
        public async Task ReadAsync_MemoryOverload_ShouldReadCorrectly()
        {
            using var ms = new MemoryStream(new byte[] {1, 2, 3, 4, 5});
            var ns = new NormalStream(ms);
            var buffer = new byte[5];
            var memory = new Memory<byte>(buffer);
            var bytesRead = await ns.ReadAsync(memory, CancellationToken.None);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(buffer, Is.EqualTo(new byte[] {1, 2, 3, 4, 5}));
        }

        [Test]
        public async Task DisposeAsync_ShouldDisposeStream()
        {
            var ms = new MemoryStream();
            var ns = new NormalStream(ms);
            await ns.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => ms.WriteByte(1));
        }

        [Test]
        public void Dispose_ShouldDisposeStream()
        {
            var ms = new MemoryStream();
            var ns = new NormalStream(ms);
            ns.Dispose();
            Assert.Throws<ObjectDisposedException>(() => ms.WriteByte(1));
        }

        [Test]
        public async Task IStream_ReadAsync_ByteArray_ShouldReadCorrectly()
        {
            using var ms = new MemoryStream(new byte[] {10, 20, 30, 40, 50});
            IStream ns = new NormalStream(ms);
            var buffer = new byte[5];
            var bytesRead = await ns.ReadAsync(buffer, 0, 5, CancellationToken.None);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(buffer, Is.EqualTo(new byte[] {10, 20, 30, 40, 50}));
        }

        [Test]
        public async Task IStream_ReadAsync_Memory_ShouldReadCorrectly()
        {
            using var ms = new MemoryStream(new byte[] {11, 22, 33, 44, 55});
            IStream ns = new NormalStream(ms);
            var buffer = new byte[5];
            var memory = new Memory<byte>(buffer);
            var bytesRead = await ns.ReadAsync(memory, CancellationToken.None);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(buffer, Is.EqualTo(new byte[] {11, 22, 33, 44, 55}));
        }

        [Test]
        public async Task ReadAsync_ByteArray_ShouldReadCorrectly()
        {
            using var ms = new MemoryStream(new byte[] {100, 101, 102, 103, 104});
            var ns = new NormalStream(ms);
            var buffer = new byte[5];
            var bytesRead = await ns.ReadAsync(buffer, 0, 5, CancellationToken.None);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(buffer, Is.EqualTo(new byte[] {100, 101, 102, 103, 104}));
        }

        [Test]
        public async Task ReadAsync_Memory_ShouldReadCorrectly()
        {
            using var ms = new MemoryStream(new byte[] {110, 120, 130, 140, 150});
            var ns = new NormalStream(ms);
            var buffer = new byte[5];
            var memory = new Memory<byte>(buffer);
            var bytesRead = await ns.ReadAsync(memory, CancellationToken.None);
            Assert.That(bytesRead, Is.EqualTo(5));
            Assert.That(buffer, Is.EqualTo(new byte[] {110, 120, 130, 140, 150}));
        }
    }
} 