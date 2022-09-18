using System;
using System.Globalization;
using OctaneEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            var config = new OctaneConfiguration
            {
                Parts = 2,
                BufferSize = 8192,
                ShowProgress = true,
                BytesPerSecond = 1,
                UseProxy = false,
                Proxy = null,
                DoneCallback = x => { Console.WriteLine("Done!"); },
                ProgressCallback = x => { Console.WriteLine(x.ToString(CultureInfo.InvariantCulture)); },
                NumRetries = 10
            };

            Engine.DownloadFile("https://www.google.com/images/branding/googlelogo/1x/googlelogo_light_color_272x92dp.png", null, config).Wait();
        }
    }
}