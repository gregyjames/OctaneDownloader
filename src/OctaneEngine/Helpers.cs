using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OctaneEngineCore.Implementations.NetworkAnalyzer;

namespace OctaneEngineCore;

public static partial class Helpers
{
    internal static List<ValueTuple<long, long>> CreatePartsList(
        long responseLength,
        int parts,
        ILogger logger,
        long minPartSize = 1 * 1024 * 1024,
        long maxChunkSize = 8 * 1024 * 1024,
        long startOffset = 0)
    {
        if (parts <= 0) throw new ArgumentException("Parts must be positive", nameof(parts));
        if (responseLength <= 0) throw new ArgumentException("Response length must be positive", nameof(responseLength));

        // Determine the optimal chunk count:
        // 1. Don't split tiny files into chunks smaller than minPartSize.
        // 2. Split large files into <= maxChunkSize work units so concurrent workers
        //    pull chunks dynamically and avoid straggler tail latency.
        long effectiveMinPartSize = minPartSize > 0 ? minPartSize : 1;
        long effectiveMaxChunkSize = maxChunkSize > 0
            ? Math.Max(maxChunkSize, effectiveMinPartSize)
            : 0;

        int chunkCount;
        if (responseLength <= effectiveMinPartSize)
        {
            chunkCount = 1;
        }
        else if (effectiveMaxChunkSize > 0 && responseLength > (long)parts * effectiveMaxChunkSize)
        {
            chunkCount = (int)Math.Min(int.MaxValue, (responseLength + effectiveMaxChunkSize - 1) / effectiveMaxChunkSize);
        }
        else
        {
            int maxByMinSize = (int)Math.Min(int.MaxValue, responseLength / effectiveMinPartSize);
            chunkCount = Math.Max(1, Math.Min(parts, maxByMinSize));
        }

        var pieces = new List<ValueTuple<long, long>>(chunkCount);
        long basePartSize = responseLength / chunkCount;

        if (logger.IsEnabled(LogLevel.Information))
        {
            LogPartSizePartsize(logger, NetworkAnalyzer.PrettySize(basePartSize));
        }

        long remainder = responseLength % chunkCount;
        long start = startOffset;
        bool traceEnabled = logger.IsEnabled(LogLevel.Trace);

        for (int i = 0; i < chunkCount; i++)
        {
            // Distribute the remainder: the first 'remainder' chunks get an extra byte
            long thisPartSize = basePartSize + (i < remainder ? 1 : 0);
            long end = start + thisPartSize - 1; // inclusive
            pieces.Add((start, end));
            if (traceEnabled)
            {
                LogPieceAddedToQueue(logger, start, end);
            }
            start = end + 1;
        }

        return pieces;
    }

    internal static Exception GetFirstRealException(Exception exception)
    {
        if (exception == null)
        {
            return null;
        }

        var current = exception;

        while (true)
        {
            if (current is AggregateException aggEx)
            {
                // Flatten aggregates (in case we have multiple or nested AggregateExceptions)
                var flattened = aggEx.Flatten();

                // If after flattening there are no inner exceptions, we're done
                if (flattened?.InnerExceptions?.Count == 0)
                {
                    break;
                }

                // Take the *first* of the flattened exceptions
                // (If you want to handle multiple, you'd iterate or choose otherwise)
                current = flattened?.InnerExceptions[0];
            }
            else if (current?.InnerException != null)
            {
                // Move to the next inner exception until there's none
                current = current.InnerException;
            }
            else
            {
                // No further nesting
                break;
            }
        }

        return current;
    }

    [LoggerMessage(LogLevel.Information, "PART SIZE: {partSize}")]
    static partial void LogPartSizePartsize(this ILogger logger, string partSize);

    [LoggerMessage(LogLevel.Trace, "Piece with range ({start},{end}) added to tasks queue.")]
    static partial void LogPieceAddedToQueue(this ILogger logger, long start, long end);
}
