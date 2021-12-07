using System;
using OctaneEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            var s = Engine.DownloadFile("https://speed.hetzner.de/1GB.bin", Environment.ProcessorCount, 8192, true);
            s.Wait();
        }
    }
}