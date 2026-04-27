using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
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

    public static string PrettySize(long len)
    {
        int order = 0;
        double size = len;
        while (size >= 1024 && order < Sizes.Length - 1)
        {
            order++;
            size /= 1024.0;
        }
            
        string result = ZString.Format("{0:0.##} {1}", size, Sizes[order]);
            
        return result;
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

        // Optimization: Use CopyToAsync(Stream.Null) with a 1MB buffer to minimize allocations and system call overhead.
        await stream.CopyToAsync(Stream.Null, 1024 * 1024).ConfigureAwait(false);

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