using OctaneDownloadEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            var s = OctaneEngine.DownloadFile(
                    "https://release.axocdn.com/win64/GitKrakenSetup.exe", 128, "out.exe");

            s.Wait();

        }
    }
}