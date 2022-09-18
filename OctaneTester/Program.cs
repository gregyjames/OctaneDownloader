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
                DoneCallback = x =>
                {
                    Console.WriteLine("Done!");
                },
                ProgressCallback = x =>
                {
                    //Console.WriteLine(x.ToString(CultureInfo.InvariantCulture));
                },
                NumRetries = 10
            };

            Engine.DownloadFile("https://az764295.vo.msecnd.net/stable/c3511e6c69bb39013c4a4b7b9566ec1ca73fc4d5/VSCodeUserSetup-x64-1.67.2.exe", null, config).Wait();
        }
    }
}