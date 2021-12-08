using System;
using OctaneEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            Engine.DownloadFile("https://speed.hetzner.de/1GB.bin", Environment.ProcessorCount, 8192, true, null!, new Action<bool>(
                x => { Console.WriteLine(x ? "Done!" : "Download failed!"); })).Wait();
        }
    }
}