using System;
using System.IO;
using System.Net.Http;
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
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Network error during download: {ex.Message}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"File I/O error during download: {ex.Message}");
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine($"Download was canceled: {ex.Message}");
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException || ex is StackOverflowException || ex is ThreadAbortException))
        {
            Console.WriteLine($"Unexpected error during download: {ex.Message}");
        }
    }
} 