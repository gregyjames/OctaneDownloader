using OctaneEngine;

namespace OctaneTester
{
    internal static class Program
    {
        private static void Main()
        {
            var s = Engine.DownloadFile(
                    "https://bitport.io/my-files/download/zq2lly7320nqzwfa2dzbq2okek7yxr44/jxpdd1dcpu", 8);

            s.Wait();

        }
    }
}