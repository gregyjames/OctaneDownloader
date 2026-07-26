using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cysharp.Text;
using OctaneEngineCore.Interfaces.NetworkAnalyzer;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("OctaneTestProject")]

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

    public static string PrettySize(long len)
    {
        if (len < 1024)
        {
            return ZString.Format("{0} B", len);
        }
        if (len < 1048576)
        {
            return ZString.Format("{0:0.##} KB", (double)len / 1024.0);
        }
        if (len < 1073741824)
        {
            return ZString.Format("{0:0.##} MB", (double)len / 1048576.0);
        }
        if (len < 1099511627776L)
        {
            return ZString.Format("{0:0.##} GB", (double)len / 1073741824.0);
        }
        return ZString.Format("{0:0.##} TB", (double)len / 1099511627776.0);
    }
    
    public static (string,int) GetTestFile(TestFileSize size)
    {
        var url = size switch
        {
            TestFileSize.Small => ("https://freetestdata.com/wp-content/uploads/2022/02/Free_Test_Data_1MB_MP4.mp4", 1000000),
            TestFileSize.Medium => ("https://freetestdata.com/wp-content/uploads/2022/02/Free_Test_Data_7MB_MP4.mp4", 7000000),
            TestFileSize.Large => ("https://freetestdata.com/wp-content/uploads/2022/02/Free_Test_Data_15MB_MP4.mp4", 15000000),
            _ => ("",0)
        };

        return url;
    }
    internal static async Task<int> GetNetworkLatency(IPingService service)
    {
        // Measure the network latency by pinging a fast server
        const string pingUrl = "www.google.com";
        var reply = await service.SendPingAsync(pingUrl);
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
    internal static async Task<int> GetNetworkSpeed((string,int) testFile, IHttpDownloader downloader)
    {
        // Measure the network speed by downloading a test file from a fast server
        using var client = new HttpClient();
        var sw = Stopwatch.StartNew();

        // Use streaming to avoid large heap allocations (LOH) when downloading test files
        using var response = await client.GetAsync(testFile.Item1, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();

        // Optimization: Increased buffer size from 8KB to 1MB to reduce system call overhead
        // and improve throughput during network speed testing.
        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(1024 * 1024);
        try
        {
            while (await stream.ReadAsync(buffer, 0, buffer.Length) > 0)
            {
                // Discard data
            }
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
        }

        sw.Stop();
        // Time to download the test file in seconds.
        var downloadTime = sw.Elapsed.TotalSeconds;
        var downloadSize = testFile.Item2;
        var networkSpeed = (int)Math.Round(downloadSize / downloadTime);
        return networkSpeed;
    }
    public static async Task<string> GetCurrentNetworkLatency(IPingService service)
    {
        return $"{await GetNetworkLatency(service)}ms";
    }
    public static async Task<string> GetCurrentNetworkSpeed(IHttpDownloader downloader)
    {
        var speed = await GetNetworkSpeed(GetTestFile(TestFileSize.Medium), downloader);
        return $"{ Convert.ToInt32((speed) / 1000000)} Mb/s";
    }
}