using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Cysharp.Text;

namespace OctaneEngineCore;

public static class NetworkAnalyzer
{
    private static readonly string[] Sizes = { "B", "KB", "MB", "GB", "TB" };

    public static string PrettySize(long len)
    {
        int order = 0;
        while (len >= 1024 && order < Sizes.Length - 1)
        {
            order++;
            len = len >> 10;
        }
            
        string result = ZString.Format("{0:0.##} {1}", len, Sizes[order]); 
            
        return result;
    }

    public enum TestFileSize
    {
        Small,
        Medium,
        Large
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
    internal static async Task<int> GetNetworkLatency()
    {
        // Measure the network latency by pinging a fast server
        const string pingUrl = "www.google.com";
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(pingUrl);
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
    internal static async Task<int> GetNetworkSpeed((string,int) testFile)
    {
        // Measure the network speed by downloading a test file from a fast server
        using var client = new HttpClient();
        var sw = Stopwatch.StartNew();
        await client.GetByteArrayAsync(testFile.Item1);
        sw.Stop();
        // Time to download the test file in seconds.
        var downloadTime = sw.Elapsed.TotalSeconds;
        // 100 KB
        var downloadSize = testFile.Item2;
        var networkSpeed = (int)Math.Round(downloadSize / downloadTime);
        return networkSpeed;
    }
    public static async Task<string> GetCurrentNetworkLatency()
    {
        return $"{await GetNetworkLatency()}ms";
    }
    public static async Task<string> GetCurrentNetworkSpeed()
    {
        var speed = await GetNetworkSpeed(GetTestFile(TestFileSize.Medium));
        return $"{ Convert.ToInt32((speed) / 1000000)} Mb/s";
    }
}