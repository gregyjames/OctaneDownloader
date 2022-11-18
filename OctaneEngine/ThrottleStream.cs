using System;
using System.IO;
using System.Reactive.Concurrency;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace OctaneEngine
{
    public class ThrottleStream: Stream
    {
        private readonly Stream _parentStream;
        private readonly int _maxBps;
        private readonly IScheduler _scheduler;
        private readonly IStopwatch _stopwatch;
        
        private long _processed;
        private readonly ILogger<ThrottleStream> _log;

        public ThrottleStream(Stream parent, int maxBytesPerSecond, ILoggerFactory factory)
        {
            this._maxBps = maxBytesPerSecond;
            this._parentStream = parent;
            this._scheduler = Scheduler.Immediate;
            this._log = factory.CreateLogger<ThrottleStream>(); 
            _stopwatch = _scheduler.StartStopwatch();
            _processed = 0;
        }

        protected void Throttle(int bytes)
        {
            _processed += bytes;
            _log.LogTrace($"Throttle stream processed {_processed} bytes.");
            var targetTime = TimeSpan.FromSeconds((double)_processed / _maxBps);
            var actualTime = _stopwatch.Elapsed;
            var sleep = targetTime - actualTime;
            if (sleep > TimeSpan.Zero)
            {
                using var waitHandle = new AutoResetEvent(initialState: false);
                _scheduler.Sleep(sleep).GetAwaiter().OnCompleted(() => waitHandle.Set());
                waitHandle.WaitOne();
            }
        }
        public override void Flush()
        {
            _parentStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _parentStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _parentStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _log.LogTrace("Throttle stream read.");
            var read = _parentStream.Read(buffer, offset, count);
            Throttle(read);
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _log.LogTrace("Throttle stream write.");
            Throttle(count);
            _parentStream.Write(buffer, offset, count);
        }

        public override bool CanRead => _parentStream.CanRead;
        public override bool CanSeek => _parentStream.CanSeek;
        public override bool CanWrite => _parentStream.CanWrite;
        public override long Length => _parentStream.Length;

        public override long Position
        {
            get => _parentStream.Position;
            set => _parentStream.Position = value;
        }
    }
    
}
