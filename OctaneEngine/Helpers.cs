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
    
    internal static CancellationToken CreateCancellationToken(CancellationTokenSource cancelTokenSource, OctaneConfiguration config, string outFile)
    {
        var cancellation_token = cancelTokenSource?.Token ?? new CancellationToken();
        cancellation_token.Register(new Action(() =>
        {
            config.DoneCallback?.Invoke(false);
            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }
        }));

        return cancellation_token;
    }
}