using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using OctaneEngine;
using ShellProgressBar;

// ReSharper disable All

namespace OctaneEngine
{
    public static class Engine
    {
        public async static Task DownloadFile(string url, int parts, string outFile = null!, Action<int> progressCallback = null!)
        {
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 10000;
            
            //Get response length and calculate part sizes
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var partSize = (long)Math.Floor(responseLength / (parts + 0.0));
            var pieces = new List<FileChunk>();
            var uri = new Uri(url);
            
            Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
            Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");
            
            //Get outfile name from argument or URL
            string filename = outFile ?? Path.GetFileName(uri.LocalPath);

            //Create memory mapped file to hold the file
            var mmf = MemoryMappedFile.CreateFromFile(filename, FileMode.OpenOrCreate, null, responseLength, MemoryMappedFileAccess.ReadWrite);

            //HTTP Client pool so we don't have to keep making them
            var httpPool = new ObjectPool<HttpClient>(() => new HttpClient(new RetryHandler(new HttpClientHandler(), 10)) {MaxResponseContentBufferSize = 1000000000});
            
            //Loop to add all the events to the queue
            for (long i = 0; i < responseLength; i += partSize)
            {
                pieces.Add(i + partSize < responseLength ? new FileChunk(i, i + partSize) : new FileChunk(i, responseLength));
            }
            
            //Options for progress base
            var options = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };
            using (var pbar = new ProgressBar(parts, "Downloading File...", options))
            {
                await Parallel.ForEachAsync(pieces, new ParallelOptions(), async (piece, cancellationToken) =>
                {
                    //Get a http client from the pool and request for the content range
                    var client = httpPool.Get();
                    var request = new HttpRequestMessage { RequestUri = new Uri(url) };
                    request.Headers.Range = new RangeHeaderValue(piece.Start, piece.End);

                    //Request headers so we dont cache the file into memory
                    var message = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    if (message.IsSuccessStatusCode)
                    {
                        //Get the content stream from the message request
                        await using var streamToRead = await message.Content.ReadAsStreamAsync(cancellationToken);
                        //Create a memory mapped stream to the mmf with the piece offset and size equal to the response size
                        var streams = mmf.CreateViewStream(piece.Start, message.Content.Headers.ContentLength!.Value,
                            MemoryMappedFileAccess.ReadWrite);
                        //Copy from the content stream to the mmf stream
                        var T = streamToRead.CopyToAsync(streams, cancellationToken);
                        await T.WaitAsync(cancellationToken);
                        //If done, update progress, return http client to pool, and flush/close mmf stream
                        if (T.IsCompletedSuccessfully)
                        {
                            pbar.Tick();
                            httpPool.Return(client);
                            streams.Flush();
                            streams.Close();
                        }
                    }
                });
            }
        }
    }
}