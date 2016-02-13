using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace OctaneDownloadEngine
{
    public class OctaneEngine
    {
        public OctaneEngine()
        {
            ServicePointManager.DefaultConnectionLimit = 10000;
        }

        public void SplitDownload(string URL, string OUT, double Parts)
        {
            try
            {
                Parallel.Invoke(() => Download(URL, OUT, Parts));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                throw;
            }
        }

        internal async void Download(string URL, string OUT, double Parts)
        {
            var responseLength = WebRequest.Create(URL).GetResponse().ContentLength;
            var partSize = (long)Math.Floor(responseLength / Parts);

            Console.WriteLine(responseLength.ToString(CultureInfo.InvariantCulture) + " TOTAL SIZE");
            Console.WriteLine(partSize.ToString(CultureInfo.InvariantCulture) + " PART SIZE" + "\n");

            var previous = 0;

            var fs = new FileStream(OUT, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, (int)partSize);
            
            try
            {
                fs.SetLength(responseLength);

                List<Tuple<Task<byte[]>, int, int>> asyncTasks = new List<Tuple<Task<byte[]>, int, int>>();

                //Parallel.For((int)partSize, responseLength + partSize, async index => {
                for(int i = (int)partSize; i < responseLength + partSize; i++){
                    var previous2 = previous;
                    var i2 = (int)i;

                    // GetResponseAsync deadlocks for some reason so switched to HttpClient instead
                    HttpClient client = new HttpClient() { MaxResponseContentBufferSize = 10000000 };

                    client.DefaultRequestHeaders.Range = new RangeHeaderValue(previous2, i2);
                    byte[] urlContents = await client.GetByteArrayAsync(URL);

                    // start each download task and keep track of them for later
                    Console.WriteLine("start {0},{1}", previous2, i2);

                    var downloadTask = client.GetByteArrayAsync(URL);
                    asyncTasks.Add(new Tuple<Task<byte[]>, int, int>(downloadTask, previous2, i2));

                    previous = i2;

                    i = (i + (int)partSize) + 1;
                }

                // now that all the downloads are started, we can await the results
                // loop through looking for a completed task in case they complete out of order
                while (asyncTasks.Count > 0)
                {
                    Tuple<Task<byte[]>, int, int> completedTask = null;

                    Parallel.ForEach(asyncTasks, async (task, state) => {
                        // as each task completes write the data to the file
                        if (task.Item1.IsCompleted)
                        {
                            //Console.WriteLine("await {0},{1}", task.Item2, task.Item3);
                            var array = await task.Item1;

                            Console.WriteLine("write to file {0},{1}", task.Item2, task.Item3);
                            fs.Position = task.Item2;

                            foreach (byte x in array)
                            {
                                if (fs.Position != task.Item3)
                                {
                                    fs.WriteByte(x);
                                }
                            }

                            completedTask = task;
                            state.Break();
                        }
                    });

                    asyncTasks.Remove(completedTask);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                fs.Close();
            }
        }
    }
}