using System;
using System.Threading;
using OctaneEngine;
using OctaneEngineCore;

namespace OctaneTester;

public class SimpleNoDITest
{
    public static void RunTest()
    {
        Console.WriteLine("Testing Octane without Dependency Injection...");
        
        const string url = "https://httpbin.org/bytes/1024"; // Small test file
        
        try
        {
            // Create engine using builder pattern - no DI required
            var engine = EngineBuilder.Create(config =>
            {
                config.Parts = 2;
                config.BufferSize = 8192;
                config.ShowProgress = false; // Disable progress bar for clean output
                config.NumRetries = 3;
                config.BytesPerSecond = 1;
                config.UseProxy = false;
                config.LowMemoryMode = false;
            }).Build();
            
            // Setup download
            var pauseTokenSource = new PauseTokenSource();
            using var cancelTokenSource = new CancellationTokenSource();
            
            Console.WriteLine($"Starting download from: {url}");
            
            // Download the file
            engine.DownloadFile(new OctaneRequest(url, "test-download.bin"), pauseTokenSource, cancelTokenSource).Wait();
            
            Console.WriteLine("Download completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during download: {ex.Message}");
        }
    }
} 