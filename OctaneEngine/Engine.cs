using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Threading;

// ReSharper disable All

namespace OctaneDownloadEngine
{
    public class OctaneEngine
    {
        protected OctaneEngine()
        {
            ServicePointManager.DefaultConnectionLimit = 10000;
        }

        public static async void SplitDownloadArray(string url, double parts, Action<byte[]> callback)
        {
            try
            {
                await DownloadByteArray(url, parts, callback).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw;
            }
        }

        public static void SplitDownloadArray(string[] urls, double parts, Action<byte[]> callback)
        {
            var tasks = new List<Task>();

            Parallel.ForEach(urls, url =>
            {
                tasks.Add(DownloadByteArray(url, parts, callback));
            });

            Task.WaitAll(tasks.ToArray());
        }

        static int TasksDone = 0;

        private async static Task DownloadByteArray(string url, double parts, Action<byte[]> callback)
        {
            var responseLength = (await WebRequest.Create(url).GetResponseAsync()).ContentLength;
            var partSize = (long)Math.Floor(responseLength / parts);

            Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
            Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");

            var ms = new MemoryStream();
            try
            {
                //Using our custom progressbar
                using (var progress = new ProgressBar())
                {
                    ms.SetLength(responseLength);

                    //Using custom concurrent queue to implement Enqueue and Dequeue Events
                    var asyncTasks = new EventfulConcurrentQueue<Tuple<Task<byte[]>, int, int>>();

                    //Delegate for Dequeue
                    asyncTasks.ItemDequeued += delegate
                    {
                        //Tasks done holds the count of the tasks done
                        //Parts *2 because there are parts number of Enqueue AND Dequeue operations
                        progress.Report(TasksDone / (parts*2));
                        Thread.Sleep(20);
                    };

                    //Delagate for Enqueue
                    asyncTasks.ItemEnqueued += delegate
                    {
                        progress.Report(TasksDone / (parts * 2));
                        Thread.Sleep(20);
                    };

                    // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
                    var client = new HttpClient(
                        //Use our custom Retry handler, with a max retry value of 10
                        new RetryHandler(new HttpClientHandler(), 10)) {MaxResponseContentBufferSize = 1000000000};

                    //Variable to hold the old loop end
                    var previous = 0;

                    //Loop to add all the events to the queue
                    for (var i = (int) partSize; i <= responseLength; i += (int) partSize)
                    {
                        //Start and end values for the chunk
                        var start = previous;
                        var current_end = i;

                        //Open a http request with the range
                        var request = new HttpRequestMessage {RequestUri = new Uri(url)};
                        request.Headers.Range = new RangeHeaderValue(start, current_end);

                        //Send the request
                        var downloadTask = client.SendAsync(request);

                        //Add the task to the queue along with the start and end value
                        asyncTasks.Enqueue(
                            new Tuple<Task<byte[]>, int, int>(downloadTask.Result.Content.ReadAsByteArrayAsync(),
                                start, current_end));

                        //Use interlocked to increment Tasks done by one
                        Interlocked.Add(ref OctaneEngine.TasksDone, 1);

                        //Set the start of the next loop to be the current end
                        previous = current_end;
                    }

                    // now that all the downloads are started, we can await the results
                    // loop through looking for a completed task in case they complete out of order
                    while (asyncTasks.Count > 0)
                    {
                        Parallel.ForEach(asyncTasks.Queue, async (task, state) =>
                        {
                            // as each task completes write the data to the file
                            if (task.Item1.IsCompleted)
                            {
                                var array = await task.Item1.ConfigureAwait(false);

                                lock (ms)
                                {
                                    ms.Position = task.Item2;
                                    ms.Write(array, 0, array.Length);
                                    asyncTasks.TryDequeue(out task);
                                    Interlocked.Add(ref TasksDone, 1);
                                }

                            }
                        });
                    }
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