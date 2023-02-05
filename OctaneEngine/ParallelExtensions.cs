/*
 * The MIT License (MIT)
 * Copyright (c) 2015 Greg James
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#if NET461 || NET472
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dasync.Collections;

namespace OctaneEngine
{
    internal static class Parallel
    {
        /// <summary>
        ///     Executes a for each operation on an <see cref="System.Collections.Generic.IEnumerable{TSource}" /> in which
        ///     iterations may run in parallel.
        /// </summary>
        /// <typeparam name="TSource">The type of the data in the source.</typeparam>
        /// <param name="source">An enumerable data source.</param>
        /// <param name="parallelOptions">An object that configures the behavior of this operation.</param>
        /// <param name="body">An asynchronous delegate that is invoked once per element in the data source.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     The exception that is thrown when the <paramref name="source" />
        ///     argument or <paramref name="body" /> argument is null.
        /// </exception>
        /// <returns>A task that represents the entire for each operation.</returns>
        public static Task ForEachAsync<TSource>(IEnumerable<TSource> source, ParallelOptions parallelOptions,
            Func<TSource, CancellationToken, ValueTask> body)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (parallelOptions is null) throw new ArgumentNullException(nameof(parallelOptions));
            if (body is null) throw new ArgumentNullException(nameof(body));

            return source.ParallelForEachAsync(
                async (item, index) => { await body.Invoke(item, parallelOptions.CancellationToken); },
                parallelOptions.MaxDegreeOfParallelism,
                parallelOptions.CancellationToken);
        }
    }
}
#endif