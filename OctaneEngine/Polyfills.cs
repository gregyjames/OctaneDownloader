using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OctaneEngineCore;

public static class Polyfills
{
    public static async Task ForEachAsync<TSource>(
        this IEnumerable<TSource> source,
        ParallelOptions parallelOptions,
        Func<TSource, CancellationToken, ValueTask> body)
    {
#if NET6_0_OR_GREATER
        await Parallel.ForEachAsync(source, parallelOptions, body).ConfigureAwait(false);
#else
        var semaphore = new SemaphoreSlim(parallelOptions.MaxDegreeOfParallelism);
        var tasks = new List<Task>();

        foreach (var item in source)
        {
            await semaphore.WaitAsync(parallelOptions.CancellationToken).ConfigureAwait(false);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await body(item, parallelOptions.CancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, parallelOptions.CancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
#endif
    }

#if !NET5_0_OR_GREATER
    public static Task<Stream> ReadAsStreamAsync(this HttpContent content, CancellationToken cancellationToken)
    {
        return content.ReadAsStreamAsync();
    }
#endif

#if !NETSTANDARD2_1_OR_GREATER && !NETCOREAPP && !NET5_0_OR_GREATER
    public static Task<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
        {
            return stream.ReadAsync(segment.Array, segment.Offset, segment.Count, cancellationToken);
        }

        var array = buffer.ToArray();
        return stream.ReadAsync(array, 0, array.Length, cancellationToken).ContinueWith(t =>
        {
            new Memory<byte>(array, 0, t.Result).CopyTo(buffer);
            return t.Result;
        }, cancellationToken);
    }

    public static Task WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
        {
            return stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken);
        }

        var array = buffer.ToArray();
        return stream.WriteAsync(array, 0, array.Length, cancellationToken);
    }

    public static Task DisposeAsync(this Stream stream)
    {
        stream.Dispose();
        return Task.CompletedTask;
    }
#endif


    public static readonly Version HttpVersion20 = 
#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        HttpVersion.Version20;
#else
        new Version(2, 0);
#endif
}
