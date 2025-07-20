using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Cysharp.Text;
using OctaneEngineCore.Interfaces.NetworkAnalyzer;

[assembly: InternalsVisibleTo("OctaneTestProject, PublicKey=0024000004800000940000000602000000240000525341310004000001000100714997d77c6a386e69a9d7a09bfdce9a5fb18bc3a5f0771d8102819aa00689d635299e27f1ec7a9838e51160cae5b38035f995737386d0367745a9a0bb68e8f31e43d6448a980402f8452787b56c7bcefe556ddd048e0eb59c919521ac2ae0b05e9a2ddbf2dc10b8e02e3f70d969055597ddef49e5e2d1ad8e9ee4f7226fd5ca", AllInternalsVisible = true)]

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
        while (len >= 1024 && order < Sizes.Length - 1)
        {
            order++;
            len = len >> 10;
        }
            
        string result = ZString.Format("{0:0.##} {1}", len, Sizes[order]); 
            
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
        await client.GetByteArrayAsync(testFile.Item1);
        sw.Stop();
        // Time to download the test file in seconds.
        var downloadTime = sw.Elapsed.TotalSeconds;
        // 100 KB
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