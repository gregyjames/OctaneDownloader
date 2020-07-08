//https://github.com/erossini/ProgressBarForConsoleApplications
//
//
//

using System;
using System.Text;
using System.Threading;

namespace OctaneDownloadEngine
{
    public class ProgressBar : IDisposable, IProgress<double>
    {
        private const int BlockCount = 10;
        private readonly TimeSpan _animationInterval = TimeSpan.FromSeconds(1.0 / 8);
        private const string Animation = @"|/-\";
        private bool _showProgressBar;

        private readonly Timer _timer;

        private double _currentProgress;
        private string _currentText = string.Empty;
        private bool _disposed;
        private int _animationIndex;


        public ProgressBar(bool showProgressBar = true)
        {
            _showProgressBar = showProgressBar;
            _timer = new Timer(TimerHandler);

            // A progress bar is only for temporary display in a console window.
            // If the console output is redirected to a file, draw nothing.
            // Otherwise, we'll end up with a lot of garbage in the target file.
            if (!Console.IsOutputRedirected)
            {
                ResetTimer();
            }
        }

        public void Report(double value)
        {
            // Make sure value is in [0..1] range
            value = Math.Max(0, Math.Min(1, value));
            Interlocked.Exchange(ref _currentProgress, value);
        }

        private void TimerHandler(object state)
        {
            lock (_timer)
            {
                if (_disposed) return;


                string text;
                if (_showProgressBar)
                {
                    var progressBlockCount = (int)(_currentProgress * BlockCount);
                    var percent = (int)(_currentProgress * 100);
                    text =
                        $"[{new string('#', progressBlockCount)}{new string('-', BlockCount - progressBlockCount)}] {percent,3}% {Animation[_animationIndex++ % Animation.Length]}";
                }
                else
                {
                    text = Animation[_animationIndex++ % Animation.Length].ToString();
                }
                UpdateText(text);


                ResetTimer();
            }
        }


        private void UpdateText(string text)
        {
            // Get length of common portion
            int commonPrefixLength = 0;
            int commonLength = Math.Min(_currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == _currentText[commonPrefixLength])
            {
                commonPrefixLength++;
            }


            // Backtrack to the first differing character
            StringBuilder outputBuilder = new StringBuilder();
            outputBuilder.Append('\b', _currentText.Length - commonPrefixLength);


            // Output new suffix
            outputBuilder.Append(text.Substring(commonPrefixLength));


            // If the new text is shorter than the old one: delete overlapping characters
            int overlapCount = _currentText.Length - text.Length;
            if (overlapCount > 0)
            {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }


            Console.Write(outputBuilder);
            _currentText = text;
        }


        private void ResetTimer()
        {
            _timer.Change(_animationInterval, TimeSpan.FromMilliseconds(-1));
        }


        public void Dispose()
        {
            lock (_timer)
            {
                _disposed = true;
                UpdateText(string.Empty);
            }
        }
    }
}
