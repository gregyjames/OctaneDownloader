using System;
using System.Net;

namespace OctaneEngine;

public class OctaneConfiguration
{
    /// <summary>
    /// The number of parts to download in parallel.
    /// </summary>
    public int Parts { get; set; }

    /// <summary>
    /// The memory buffer size to use, default 8192.
    /// </summary>
    public int BufferSize { get; set; }

    /// <summary>
    /// Show a progress bar
    /// </summary>
    public bool ShowProgress { get; set; }

    /// <summary>
    /// The Action<bool> function to call when the download is finished. 
    /// </summary>
    public Action<bool> DoneCallback { get; set; }

    /// <summary>
    /// The Action<double> function to call to report download progress.
    /// </summary>
    public Action<double> ProgressCallback { get; set; }

    /// <summary>
    /// Number of times to retry if the connection fails.
    /// </summary>
    public int NumRetries { get; set; }

    /// <summary>
    /// Use this option to throttle the download of the file. Use 1 to disable throttling.
    /// </summary>
    public int BytesPerSecond { get; set; }

    /// <summary>
    /// Enable if you want to use a proxy
    /// </summary>
    public bool UseProxy { get; set; }

    /// <summary>
    /// The Proxy settings to use.
    /// </summary>
    public IWebProxy Proxy { get; set; }

    public OctaneConfiguration()
    {
        Parts = 4;
        BufferSize = 8096;
        ShowProgress = false;
        DoneCallback = null!;
        ProgressCallback = null!;
        NumRetries = 10;
        BytesPerSecond = 1;
        UseProxy = false;
        Proxy = null;
    }

    public override string ToString()
    {
        string s =
            $"Parts: {Parts}, BufferSize: {BufferSize}, ShowProgres: {ShowProgress}, ProgressCallback: {ProgressCallback != null}, NumRetries: {NumRetries}, BytesPerSecond: {BytesPerSecond}, DoneCallback: {DoneCallback != null}, Proxy: {Proxy?.ToString() ?? "NULL"}, UseProxy: {UseProxy}";
        return "{" + s + "}";
    }
}