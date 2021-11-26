using OctaneEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            var s = Engine.DownloadFile(
                    "https://www.wonderland.money/static/media/Chershire_Cat.24ee16b9.jpeg", 4);

            s.Wait();

        }
    }
}