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
        /// <param name="outFile">The output file name of the download. Use 'null' to get file name from url.</param>
        public async static Task DownloadFile(string url, string outFile = null , OctaneConfiguration config = null) {
            if (config == null) {
                config = new OctaneConfiguration();
            }
            
            if (url == null) throw new ArgumentNullException(nameof(url));
            
            var memPool = ArrayPool<byte>.Shared;
            
            //Get response length and calculate part sizes
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var partSize = (long)Math.Floor(responseLength / (config.Parts + 0.0));
            var pieces = new List<ValueTuple<long, long>>();
            var uri = new Uri(url);
            var filename = outFile ?? Path.GetFileName(uri.LocalPath);
            
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 10000;
            ServicePointManager.FindServicePoint(new Uri(url)).ConnectionLimit = config.Parts;
            
            #if NET6_0_OR_GREATER
                ServicePointManager.ReusePort = true;
            #endif
            
            Console.WriteLine("TOTAL SIZE: " + prettySize(responseLength));
            Console.WriteLine("PART SIZE: " + prettySize(partSize) + "\n");

            //Loop to add all the events to the queue
            for (long i = 0; i < responseLength; i += partSize) {
                pieces.Insert(0,(i + partSize < responseLength ? new ValueTuple<long, long>(i, i + partSize) : new ValueTuple<long, long>(i, responseLength)));
            }

            //Options for progress base
            var options = new ProgressBarOptions {
                ProgressBarOnBottom = false,
                BackgroundCharacter = '\u2593',
                DenseProgressBar = false,
                DisplayTimeInRealTime = false
            };

            #region HTTPClient Init
            var clientHandler = new HttpClientHandler()
            {
                PreAuthenticate = true,
                UseDefaultCredentials = true,
                Proxy = config.Proxy,
                UseProxy = config.UseProxy,
                MaxConnectionsPerServer = config.Parts,
                UseCookies = false,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
            var retryHandler = new RetryHandler(clientHandler, config.NumRetries);
        
            var _client = new HttpClient(retryHandler) {
                MaxResponseContentBufferSize = config.BufferSize
            };
            #endregion
            
            //Create memory mapped file to hold the file
            using (var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, responseLength, MemoryMappedFileAccess.ReadWrite)) {
                int tasksDone = 0;
                try {
                    var pbar = config.ShowProgress ? new ProgressBar(config.Parts, "Downloading File...", options) : null;
                    await Parallel.ForEachAsync(pieces, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (piece, cancellationToken) => {
                        System.Diagnostics.Trace.Listeners.Clear();
                        
                        //Get a client from the pool and request for the content range
                        var client = new OctaneClient(config, _client);
                        
                        using (var request = new HttpRequestMessage { RequestUri = new Uri(url) }) {
                            request.Headers.Range = new RangeHeaderValue(piece.Item1, piece.Item2);
                                            
                            //Request headers so we dont cache the file into memory
                            if (client != null) {
                                var message = client.SendMessage(url, piece, cancellationToken).Result;
                                await client.ReadResponse(message, mmf, piece, memPool, cancellationToken, pbar);
                            }

                            Interlocked.Increment(ref tasksDone);
                        }
                    });
                }
                catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                    config.DoneCallback(false);
                }
                finally {
                    _client.Dispose();
                    config.DoneCallback(true);
                }
            }
        }
    }
}