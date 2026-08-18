using BenchmarkDotNet.Running;

namespace BenchmarkOctaneProject
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<DownloadBenchmark>();
        }
    }
}
