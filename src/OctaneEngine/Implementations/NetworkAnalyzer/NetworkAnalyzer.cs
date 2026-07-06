using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Text;
using OctaneEngineCore.Interfaces.NetworkAnalyzer;

[assembly: InternalsVisibleTo("OctaneTestProject")]

namespace OctaneEngineCore.Implementations.NetworkAnalyzer;

public enum TestFileSize
{
    Small,
    Medium,
    Large
}

internal static class NetworkAnalyzer
{
    private static readonly string[] Sizes = { "B", "KB", "MB", "GB", "TB" };
    internal static readonly HttpClient SharedClient = CreateSharedClient();

    private static HttpClient CreateSharedClient()
    {
#if NETCOREAPP || NET5_0_OR_GREATER
        return new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        });
#else
        return new HttpClient();
#endif
    }

    public static string PrettySize(long len)
    {
        int order = 0;
        double size = len;
        while (size >= 1024 && order < Sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
            
        string result = ZString.Format("{0:0.##} {1}", size, Sizes[order]);
            
        return result;
    }
    
    public static (string url, int size) GetTestFile(TestFileSize size)
    {
        return size switch
        {
            TestFileSize.Small => (url: "https://freetestdata.com/wp-content/uploads/2022/02/Free_Test_Data_1MB_MP4.mp4", size: 1000000),
            TestFileSize.Medium => (url: "https://freetestdata.com/wp-content/uploads/2022/02/Free_Test_Data_7MB_MP4.mp4", size: 7000000),
            TestFileSize.Large => (url: "https://freetestdata.com/wp-content/uploads/2022/02/Free_Test_Data_15MB_MP4.mp4", size: 15000000),
            _ => (url: "", size: 0)
        };
    }
    internal static async Task<int> GetNetworkLatency(IPingService service)
    {
        // Measure the network latency by pinging a fast server
        const string pingUrl = "www.google.com";
        var reply = await service.SendPingAsync(pingUrl).ConfigureAwait(false);
        if (reply?.Status == IPStatus.Success)
        {
            var latency = (int)reply.RoundtripTime;
            return latency;
        }
        else
        {
            throw new Exception("Unable to ping server: " + reply?.Status);
        }
    }
    internal static async Task<int> GetNetworkSpeed((string url, int size) testFile, CancellationToken cancellationToken = default)
    {
        // Measure the network speed by downloading a test file from a fast server
        var sw = Stopwatch.StartNew();

        // Use streaming to avoid large heap allocations (LOH) when downloading test files
        // Reuse SharedClient to benefit from connection pooling
        using var response = await SharedClient.GetAsync(testFile.url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        // Optimization: Use CopyToAsync to Stream.Null with a large buffer for efficient data discarding
        // and reduced system call overhead compared to manual read loops.
        await stream.CopyToAsync(Stream.Null, 1024 * 1024, cancellationToken).ConfigureAwait(false);

        sw.Stop();
        // Time to download the test file in seconds.
        var downloadTime = sw.Elapsed.TotalSeconds;
        var networkSpeed = (int)Math.Round(testFile.size / downloadTime);
        return networkSpeed;
    }
    public static async Task<string> GetCurrentNetworkLatency(IPingService service)
    {
        return $"{await GetNetworkLatency(service).ConfigureAwait(false)}ms";
    }
    public static async Task<string> GetCurrentNetworkSpeed(CancellationToken cancellationToken = default)
    {
        var speed = await GetNetworkSpeed(GetTestFile(TestFileSize.Medium), cancellationToken).ConfigureAwait(false);
        return $"{ Convert.ToInt32((speed) / 1000000)} Mb/s";
    }
}
