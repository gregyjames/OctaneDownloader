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

#nullable enable

namespace OctaneEngineCore;

/// <summary>
/// Represents the progress of a file download operation.
/// </summary>
public class DownloadProgress
{
    /// <summary>
    /// The total size of the file in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// The overall number of bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; init; }

    /// <summary>
    /// The overall progress percentage (from 0 to 100).
    /// </summary>
    public double ProgressPercentage { get; init; }

    /// <summary>
    /// The total number of download parts/threads.
    /// </summary>
    public int TotalParts { get; init; }

    /// <summary>
    /// The number of completed parts/threads.
    /// </summary>
    public int PartsCompleted { get; init; }

    /// <summary>
    /// The index of the part that triggered this progress update.
    /// </summary>
    public int PartIndex { get; init; }

    /// <summary>
    /// The number of bytes downloaded in the specific part that triggered this update.
    /// </summary>
    public long PartBytesDownloaded { get; init; }

    /// <summary>
    /// The total number of bytes in the specific part that triggered this update.
    /// </summary>
    public long PartTotalBytes { get; init; }

    /// <summary>
    /// Whether the specific part has finished downloading.
    /// </summary>
    public bool PartCompleted { get; init; }
}
