#if NET461 || NET472
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;

namespace System.Threading.Tasks
{

    public static partial class Parallel
    {

        /// <summary>Executes a for each operation on an <see cref="System.Collections.Generic.IEnumerable{TSource}"/> in which iterations may run in parallel.</summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="parallelOptions">An object that configures the behavior of this operation.</param>
        /// <param name="body">An asynchronous delegate that is invoked once per element in the data source.</param>
        /// <exception cref="System.ArgumentNullException">The exception that is thrown when the <paramref name="source"/> argument or <paramref name="body"/> argument is null.</exception>
        /// <returns>A task that represents the entire for each operation.</returns>
        public static Task ForEachAsync<TSource>(IEnumerable<TSource> source, ParallelOptions parallelOptions, Func<TSource, CancellationToken, ValueTask> body)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (parallelOptions is null)
            {
                throw new ArgumentNullException(nameof(parallelOptions));
            }
            if (body is null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            return source.ParallelForEachAsync(
                async (item, index) =>
                {
                    await body.Invoke(item, parallelOptions.CancellationToken);
                },
                maxDegreeOfParallelism: parallelOptions.MaxDegreeOfParallelism,
                cancellationToken: parallelOptions.CancellationToken);
        }
    }
}
#endif
