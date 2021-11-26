using OctaneEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            var s = Engine.DownloadFile(
                    "https://release.axocdn.com/win64/GitKrakenSetup.exe", 128, "out.exe");

            s.Wait();

        }
    }
}