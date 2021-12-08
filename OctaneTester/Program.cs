using System;
using OctaneEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            Engine.DownloadFile("https://speed.hetzner.de/100MB.bin", Environment.ProcessorCount, 8192, true, null!, x =>
                {
                    //Task completion action example
                    Console.WriteLine(x ? "Done!" : "Download failed!");
                },
                Console.WriteLine).Wait();
        }
    }
}