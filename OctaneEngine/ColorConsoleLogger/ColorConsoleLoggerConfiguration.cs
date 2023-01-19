using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace OctaneEngineCore.ColorConsoleLogger;

public sealed class ColorConsoleLoggerConfiguration
{
    public int EventId { get; set; }

    public Dictionary<LogLevel, ConsoleColor> LogLevelToColorMap { get; set; } = new()
    {
        [LogLevel.Information] = ConsoleColor.Green,
        [LogLevel.Warning] = ConsoleColor.Yellow,
        [LogLevel.Error] = ConsoleColor.Red,
        [LogLevel.Trace] = ConsoleColor.Gray,
        [LogLevel.Debug] = ConsoleColor.Gray
    };
}