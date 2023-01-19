using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OctaneEngineCore.ColorConsoleLogger;
public sealed class ColorConsoleLoggerProvider : ILoggerProvider
{
    private readonly ColorConsoleLoggerConfiguration _currentConfig;
    private readonly ConcurrentDictionary<string, ColorConsoleLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

    public ColorConsoleLoggerProvider(ColorConsoleLoggerConfiguration config)
    {
        _currentConfig = config;
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new ColorConsoleLogger(name, GetCurrentConfig));

    private ColorConsoleLoggerConfiguration GetCurrentConfig() => _currentConfig;

    public void Dispose()
    {
        _loggers.Clear();
    }
}