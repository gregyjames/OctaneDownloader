using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ProgressBar = OctaneEngine.ShellProgressBar.ProgressBar;
using ProgressBarOptions = OctaneEngine.ShellProgressBar.ProgressBarOptions;

// ReSharper disable All

namespace OctaneEngine
{
    public static class Engine
    {
        static readonly string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        private static string prettySize(long len)
        {
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            string result = String.Format("{0:0.##} {1}", len, sizes[order]);

            return result;
        }

        /// <summary>
        /// The core octane download function.
        /// </summary>
        /// <param name="url">The string url of the file to be downloaded.</param>
        /// <param name="parts">The number of parts (processes) needed to download the file.</param>
        /// <param name="bufferSize">The buffer size to use to download the file</param>
        /// <param name="showProgress">Show the progressbars?</param>
        /// <param name="outFile">The output file name of the download. Use 'null' to get file name from url.</param>
        /// <param name="doneCallback">Callback to handle download completion</param>
        /// <param name="progressCallback">The callback for progress if not wanting to use the built in progress</param>
        /// <param name="numRetries">Number of times to retry a failed download</param>
        /// <param name="bytesPerSecond">Throttle the file download speed if specified. 1 means unlimited.</param>
        public async static Task DownloadFile(string url, int parts, int bufferSize = 8096, bool showProgress = false,
            string outFile = null!,
            Action<Boolean> doneCallback = null!,
            Action<Double> progressCallback = null!,
            int numRetries = 10,
            int bytesPerSecond = 1)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));
            var programbps = bytesPerSecond / parts;
            
            //HTTP Client pool so we don't have to keep making them
            var httpPool = new ObjectPool<HttpClient?>(() =>
                new HttpClient(new RetryHandler(new HttpClientHandler
                    {
                        Proxy = null,
                        UseProxy = false,
                        MaxConnectionsPerServer = 256,
                        UseCookies = false
                    }, numRetries))
                    { MaxResponseContentBufferSize = bufferSize});

            var memPool = ArrayPool<byte>.Shared;
            
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 10000;

            //Get response length and calculate part sizes
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var partSize = (long)Math.Floor(responseLength / (parts + 0.0));
            var pieces = new List<ValueTuple<long, long>>();
            var uri = new Uri(url);

            Console.WriteLine("TOTAL SIZE: " + prettySize(responseLength));
            Console.WriteLine("PART SIZE: " + prettySize(partSize) + "\n");

            //Get outfile name from argument or URL
            string filename = outFile ?? Path.GetFileName(uri.LocalPath);

            //Loop to add all the events to the queue
            for (long i = 0; i < responseLength; i += partSize)
            {
                pieces.Insert(0,(i + partSize < responseLength ? new ValueTuple<long, long>(i, i + partSize) : new ValueTuple<long, long>(i, responseLength)));
            }

            //Options for progress base
            var options = new ProgressBarOptions
            {
                ProgressBarOnBottom = false,
                BackgroundCharacter = '\u2593',
                DenseProgressBar = false,
                DisplayTimeInRealTime = false
            };
            var childOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Cyan,
                BackgroundColor = ConsoleColor.Black,
                CollapseWhenFinished = true,
                DenseProgressBar = true,
                BackgroundCharacter = '\u2591',
                DisplayTimeInRealTime = false
            };

            //Create memory mapped file to hold the file
            using (var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, responseLength,
                MemoryMappedFileAccess.ReadWrite))
            {
                int tasksDone = 0;
                //var pieceCounts = Enumerable.Range(0, parts);
                try
                {
                    if (showProgress)
                    {
                        using (var pbar = new ProgressBar(parts, "Downloading File...", options))
                        {
                            await Parallel.ForEachAsync(pieces,
                                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                                async (piece, cancellationToken) =>
                                {
                                    System.Diagnostics.Trace.Listeners.Clear();
                                    //Get a http client from the pool and request for the content range

                                    var client = httpPool.Get();
                                    try
                                    {
                                        using (var request = new HttpRequestMessage { RequestUri = new Uri(url) })
                                        {

                                            request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);

                                            //Request headers so we dont cache the file into memory
                                            if (client != null)
                                            {
                                                using (var message = await client.SendAsync(request,
                                                    HttpCompletionOption.ResponseHeadersRead,
                                                    cancellationToken).ConfigureAwait(false))
                                                {

                                                    if (message.IsSuccessStatusCode)
                                                    {
                                                        //Get the content stream from the message request
                                                        using (var streamToRead = await message.Content
                                                            .ReadAsStreamAsync(cancellationToken)
                                                            .ConfigureAwait(false))
                                                        {
                                                            //Throttle stream to over BPS divided among the parts
                                                            var source = new ThrottleStream(streamToRead, programbps);
                                                            //Create a memory mapped stream to the mmf with the piece offset and size equal to the response size
                                                            using (var streams = mmf.CreateViewStream(piece.Item1,
                                                                message.Content.Headers.ContentLength!.Value,
                                                                MemoryMappedFileAccess.Write))
                                                            {
                                                                using (var chil =
                                                                    pbar.Spawn(
                                                                        (int)Math.Round(
                                                                            (double)(message.Content.Headers
                                                                                    .ContentLength /
                                                                                bufferSize)), "", childOptions))
                                                                {
                                                                    //Copy from the content stream to the mmf stream
                                                                    //var buffer = new byte[bufferSize];
                                                                    var buffer = memPool.Rent(bufferSize);

                                                                    int offset, bytesRead;
                                                                    // Until we've read everything
                                                                    do
                                                                    {
                                                                        offset = 0;
                                                                        // Until the buffer is very nearly full or there's nothing left to read
                                                                        do
                                                                        {
                                                                            if (bytesPerSecond == 1)
                                                                            {
                                                                                bytesRead = await streamToRead
                                                                                    .ReadAsync(
                                                                                        buffer, 
                                                                                        offset,
                                                                                        bufferSize - offset,
                                                                                        cancellationToken)
                                                                                    .ConfigureAwait(false);
                                                                            }
                                                                            else
                                                                            {
                                                                                bytesRead = await source.ReadAsync(
                                                                                        buffer,
                                                                                        offset,
                                                                                        bufferSize - offset,
                                                                                        cancellationToken)
                                                                                    .ConfigureAwait(false);
                                                                                offset += bytesRead;
                                                                            }
                                                                        } while (bytesRead != 0 && offset < bufferSize);

                                                                        // Empty the buffer
                                                                        if (offset != 0)
                                                                        {
                                                                            //fileStrm.Write(buffer, 0, offset);
                                                                            await streams.WriteAsync(
                                                                                    buffer, 0, offset,
                                                                                    cancellationToken)
                                                                                .ConfigureAwait(false);
                                                                            chil.Tick();
                                                                        }
                                                                    } while (bytesRead != 0);

                                                                    //Array.Clear(buffer);
                                                                    memPool.Return(buffer, false);
                                                                }

                                                                streams.Flush();
                                                                //streams.Close();
                                                            }

                                                            //streamToRead.Close();
                                                        }
                                                    }

                                                    pbar.Tick();
                                                }
                                            }

                                            Interlocked.Increment(ref tasksDone);
                                        }
                                    }
                                    finally
                                    {
                                        httpPool.Return(client);
                                    }
                                });
                        }
                    }

                    else
                    {
                        await Parallel.ForEachAsync(pieces,
                            new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                            async (piece, cancellationToken) =>
                            {
                                //Get a http client from the pool and request for the content range
                                var client = httpPool.Get();
                                try
                                {
                                    using (var request = new HttpRequestMessage { RequestUri = new Uri(url) })
                                    {
                                        request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);

                                        //Request headers so we dont cache the file into memory
                                        if (client != null)
                                        {
                                            using (var message = await client.SendAsync(request,
                                                HttpCompletionOption.ResponseHeadersRead,
                                                cancellationToken).ConfigureAwait(false))
                                            {

                                                if (message.IsSuccessStatusCode)
                                                {
                                                    //Get the content stream from the message request
                                                    using (var streamToRead = await message.Content
                                                        .ReadAsStreamAsync(cancellationToken)
                                                        .ConfigureAwait(false))
                                                    {
                                                        //Create a memory mapped stream to the mmf with the piece offset and size equal to the response size
                                                        using (var streams = mmf.CreateViewStream(piece.Item1,
                                                            message.Content.Headers.ContentLength!.Value,
                                                            MemoryMappedFileAccess.Write))
                                                        {
                                                            var source = new ThrottleStream(streamToRead, programbps);
                                                            //Copy from the content stream to the mmf stream
                                                            //var buffer = new byte[bufferSize];
                                                            var buffer = memPool.Rent(bufferSize);
                                                            int offset, bytesRead;
                                                            // Until we've read everything
                                                            do
                                                            {
                                                                offset = 0;
                                                                // Until the buffer is very nearly full or there's nothing left to read
                                                                do
                                                                {
                                                                    if (bytesPerSecond == 1)
                                                                    {
                                                                        bytesRead = await streamToRead
                                                                            .ReadAsync(
                                                                                buffer, 
                                                                                offset,
                                                                                bufferSize - offset,
                                                                                cancellationToken)
                                                                            .ConfigureAwait(false);
                                                                    }
                                                                    else
                                                                    {
                                                                        bytesRead = await source.ReadAsync(
                                                                                buffer,
                                                                                offset,
                                                                                bufferSize - offset,
                                                                                cancellationToken)
                                                                            .ConfigureAwait(false);
                                                                        offset += bytesRead;
                                                                    }
                                                                } while (bytesRead != 0 && offset < bufferSize);

                                                                // Empty the buffer
                                                                if (offset != 0)
                                                                {
                                                                    //fileStrm.Write(buffer, 0, offset);
                                                                    await streams.WriteAsync(buffer, 0, offset,
                                                                        cancellationToken);
                                                                }
                                                            } while (bytesRead != 0);

                                                            streams.Flush();
                                                            //streams.Close();
                                                            memPool.Return(buffer);
                                                        }

                                                        //streamToRead.Close();
                                                    }
                                                }
                                            }
                                        }

                                    }
                                }
                                finally
                                {
                                    Interlocked.Increment(ref tasksDone);
                                    httpPool.Return(client);
                                    progressCallback((double)((tasksDone + 0.0) / (pieces.Count + 0.0)));
                                }
                            });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    doneCallback(false);
                }
                finally
                {
                    httpPool.Empty();
                    doneCallback(true);
                }
            }
        }
    }
}