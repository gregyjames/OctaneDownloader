/*
 * The MIT License (MIT)
 * Copyright (c) 2015 Greg James
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Net;

namespace OctaneEngine;

public class OctaneConfiguration
{
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

    /// <summary>
    ///     The number of parts to download in parallel.
    /// </summary>
    public int Parts { get; set; }

    /// <summary>
    ///     The memory buffer size to use, default 8192.
    /// </summary>
    public int BufferSize { get; set; }

    /// <summary>
    ///     Show a progress bar
    /// </summary>
    public bool ShowProgress { get; set; }

    /// <summary>
    ///     The Action<bool> function to call when the download is finished.
    /// </summary>
    public Action<bool> DoneCallback { get; set; }

    /// <summary>
    ///     The Action<double> function to call to report download progress.
    /// </summary>
    public Action<double> ProgressCallback { get; set; }

    /// <summary>
    ///     Number of times to retry if the connection fails.
    /// </summary>
    public int NumRetries { get; set; }

    /// <summary>
    ///     Use this option to throttle the download of the file. Use 1 to disable throttling.
    /// </summary>
    public int BytesPerSecond { get; set; }

    /// <summary>
    ///     Enable if you want to use a proxy
    /// </summary>
    public bool UseProxy { get; set; }

    /// <summary>
    ///     The Proxy settings to use.
    /// </summary>
    public IWebProxy Proxy { get; set; }

    public override string ToString()
    {
        var s =
            $"Parts: {Parts}, BufferSize: {BufferSize}, ShowProgress: {ShowProgress}, ProgressCallback: {ProgressCallback != null}, NumRetries: {NumRetries}, BytesPerSecond: {BytesPerSecond}, DoneCallback: {DoneCallback != null}, Proxy: {Proxy?.ToString() ?? "NULL"}, UseProxy: {UseProxy}";
        return "{" + s + "}";
    }
}