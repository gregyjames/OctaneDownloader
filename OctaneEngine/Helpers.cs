using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;

namespace OctaneEngineCore;

public static class Helpers
{
    internal static List<ValueTuple<long, long>> CreatePartsList(long responseLength, int parts, ILogger logger)
    {
        var pieces = new List<ValueTuple<long, long>>(parts);
        if (parts <= 0) throw new ArgumentException("Parts must be positive", nameof(parts));
        if (responseLength <= 0) throw new ArgumentException("Response length must be positive", nameof(responseLength));

        long basePartSize = responseLength / parts;
        logger.LogInformation("PART SIZE: {partSize}", NetworkAnalyzer.PrettySize(basePartSize));

        long remainder = responseLength % parts;
        long start = 0;
        for (int i = 0; i < parts; i++)
        {
            // Distribute the remainder: the first 'remainder' parts get an extra byte
            long thisPartSize = basePartSize + (i < remainder ? 1 : 0);
            long end = start + thisPartSize - 1; // inclusive
            pieces.Add((start, end));
            logger.LogTrace($"Piece with range ({start},{end}) added to tasks queue.");
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
}