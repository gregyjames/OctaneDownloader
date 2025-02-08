using System;
using System.IO;
using System.Threading;
using Collections.Pooled;
using Microsoft.Extensions.Logging;
using OctaneEngine;

namespace OctaneEngineCore;

public static class Helpers
{
    internal static PooledList<ValueTuple<long, long>> CreatePartsList(long responseLength, long partSize, ILogger logger)
    {
        var pieces = new PooledList<ValueTuple<long, long>>();
        //Loop to add all the events to the queue
        for (long i = 0; i < responseLength; i += partSize) {
            //Increment the start by one byte for all parts but the first which starts from zero.
            if (i != 0) {
                i += 1;
            }
            var j = Math.Min(i + partSize, responseLength);
            var piece = new ValueTuple<long, long>(i, j);
            pieces.Add(piece);
            logger.LogTrace($"Piece with range ({piece.Item1},{piece.Item2}) added to tasks queue.");
        }
            
        return pieces;
    }
    
    internal static CancellationToken CreateCancellationToken(CancellationTokenSource cancelTokenSource, OctaneConfiguration config)
    {
        var cancellation_token = cancelTokenSource?.Token ?? new CancellationToken();
        return cancellation_token;
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