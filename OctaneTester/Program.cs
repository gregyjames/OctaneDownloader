using System;
using OctaneEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            Engine.DownloadFile("https://az764295.vo.msecnd.net/stable/c3511e6c69bb39013c4a4b7b9566ec1ca73fc4d5/VSCodeUserSetup-x64-1.67.2.exe", 20, 8192, false, null, x =>
                {
                    //Task completion action example
                    Console.WriteLine(x ? "Done!" : "Download failed!");
                },
                x =>
                {
                    Console.WriteLine(x);
                }).Wait();
        }
    }
}