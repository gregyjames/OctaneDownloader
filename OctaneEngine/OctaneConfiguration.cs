using System;

namespace OctaneEngine;

public class OctaneConfiguration
{
    /// <summary>
    /// The number of parts to download in parallel.
    /// </summary>
    public int Parts { get; set; }

    /// <summary>
    /// The memory buffersize to use, default 8192.
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

    public OctaneConfiguration()
    {
        Parts = 4;
        BufferSize = 8096;
        ShowProgress = false;
        DoneCallback = null!;
        ProgressCallback = null!;
        NumRetries = 10;
        BytesPerSecond = 1;
    }
}