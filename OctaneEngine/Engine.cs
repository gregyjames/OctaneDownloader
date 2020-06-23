using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Concurrent;
// ReSharper disable All

namespace OctaneDownloadEngine
{
    public class OctaneEngine
    {
        protected OctaneEngine()
        {
            ServicePointManager.DefaultConnectionLimit = 10000;
        }

        public static void SplitDownloadArray(string url, double parts, string fileOut, Action<byte[]> callback)
        {
            try
            {
                Parallel.Invoke(async () => await DownloadByteArray(url, parts, fileOut, callback).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw;
            }
        }

        private async static Task DownloadByteArray(string url, double parts, string fileOut, Action<byte[]> callback)
        {
            if (fileOut == null){throw new ArgumentNullException(nameof(fileOut));}
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var partSize = (long)Math.Floor(responseLength / parts);

            Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
            Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");

            var previous = 0;

            var ms = new MemoryStream();
            try
            {
                ms.SetLength(responseLength);
                
                ConcurrentQueue<Tuple<Task<byte[]>, int, int>> asyncTasks = new ConcurrentQueue<Tuple<Task<byte[]>, int, int>>();
                
                // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
                var client = new HttpClient{ MaxResponseContentBufferSize = 1000000000 };

                await client.GetByteArrayAsync(url);
                for (var i = (int)partSize; i < responseLength + partSize; i += (int)partSize)
                {
                    var previous2 = previous;
                    var i2 = i;

                    
                    client.DefaultRequestHeaders.Range = new RangeHeaderValue(previous2, i2);

                    // start each download task and keep track of them for later
                    Console.WriteLine("start {0},{1}", previous2, i2);

                    var downloadTask = client.GetByteArrayAsync(url);
                    asyncTasks.Enqueue(new Tuple<Task<byte[]>, int, int>(downloadTask, previous2, i2));
                    previous = i2;
                }

                // now that all the downloads are started, we can await the results
                // loop through looking for a completed task in case they complete out of order
                while(asyncTasks.Count > 0)
                {
                    Parallel.ForEach(asyncTasks, async (task, state) =>
                    {
                        // as each task completes write the data to the file
                        if (task.Item1.IsCompleted)
                        {
                            var array = await task.Item1.ConfigureAwait(false);

                            Console.WriteLine("write to file {0},{1}", task.Item2, task.Item3);

                            lock (ms)
                            {
                                ms.Position = task.Item2;
                                ms.Write(array, 0, array.Length);
                                asyncTasks.TryDequeue(out task);
                            }
                            
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                ms.Flush();
                ms.Close();
                callback?.Invoke(ms.ToArray());
            }
        }
    }
}