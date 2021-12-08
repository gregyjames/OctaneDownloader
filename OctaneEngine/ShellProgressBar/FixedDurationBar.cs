using System;
using System.Threading;

namespace OctaneEngine.ShellProgressBar
{
    public class FixedDurationBar : ProgressBar
    {
        public bool IsCompleted { get; private set; }

        private readonly ManualResetEvent _completedHandle = new(false);
        public WaitHandle CompletedHandle => _completedHandle;

        public FixedDurationBar(TimeSpan duration, string message, ConsoleColor color) : this(duration, message,
            new ProgressBarOptions { ForegroundColor = color })
        {
        }

        public FixedDurationBar(TimeSpan duration, string message, ProgressBarOptions options = null)
            : base((int)Math.Ceiling(duration.TotalSeconds) * 2, message, options)
        {
            if (!Options.DisplayTimeInRealTime)
                throw new ArgumentException(
                    $"{nameof(ProgressBarOptions)}.{nameof(ProgressBarOptions.DisplayTimeInRealTime)} has to be true for {nameof(FixedDurationBar)}",
                    nameof(options)
                );
        }

        private long _seenTicks = 0;

        protected override void OnTimerTick()
        {
            Interlocked.Increment(ref _seenTicks);
            Tick();
            base.OnTimerTick();
        }

        protected override void OnDone()
        {
            IsCompleted = true;
            _completedHandle.Set();
        }
    }
}