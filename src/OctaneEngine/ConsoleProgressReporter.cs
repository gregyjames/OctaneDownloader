using System;
using System.Collections.Concurrent;
using OctaneEngineCore.ShellProgressBar;
namespace OctaneEngineCore;

public class ConsoleProgressReporter : IProgress<DownloadProgress>, IDisposable
{
    private readonly OctaneConfiguration _config;
    private ProgressBar? _mainProgressBar;
    private readonly ConcurrentDictionary<int, ChildProgressBar> _childBars = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    private static readonly ProgressBarOptions MainProgressBarOptions = new()
    {
        ProgressBarOnBottom = false,
        BackgroundCharacter = '\u2593',
        DenseProgressBar = false,
        DisplayTimeInRealTime = false
    };

    private static readonly ProgressBarOptions ChildProgressBarOptions = new()
    {
        CollapseWhenFinished = true,
        DisplayTimeInRealTime = false,
        BackgroundColor = ConsoleColor.Magenta,
        DenseProgressBar = true,
        DisableBottomPercentage = true,
        ShowEstimatedDuration = true
    };

    public ConsoleProgressReporter(OctaneConfiguration config)
    {
        _config = config;
    }

    public void Report(DownloadProgress value)
    {
        if (!_config.ShowProgress)
        {
            return;
        }

        lock (_lock)
        {
            if (_isDisposed) return;

            // Initialize the main progress bar on the first update
            if (_mainProgressBar == null)
            {
                // We tick the main progress bar once when a part completes
                _mainProgressBar = new ProgressBar(value.TotalParts, "Downloading file...", MainProgressBarOptions);
            }

            // For DefaultClient or single-part downloads, just update the main progress bar
            if (value.TotalParts <= 1)
            {
                var percentage = (int)Math.Round(value.ProgressPercentage);
                _mainProgressBar.Tick(percentage);
                return;
            }

            // For multi-part parallel downloads, manage child progress bars
            var child = _childBars.GetOrAdd(value.PartIndex, index => 
                _mainProgressBar.Spawn(value.PartTotalBytes, $"Downloading part {index + 1}...", ChildProgressBarOptions));

            if (value.PartCompleted)
            {
                child.Tick(value.PartTotalBytes);
                child.Dispose();
                _mainProgressBar.Tick($"Completed part {value.PartIndex + 1}");
            }
            else
            {
                child.Tick(value.PartBytesDownloaded);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var child in _childBars.Values)
            {
                child.Dispose();
            }
            _childBars.Clear();

            _mainProgressBar?.Dispose();
            _mainProgressBar = null;
        }
    }
}
