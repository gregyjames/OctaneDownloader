# OctaneDownloader Performance Optimization: RandomAccess

## Context
OctaneDownloader is a high-performance C# asynchronous file downloader. Currently, it orchestrates parallel piece downloads using `MemoryMappedFile` and `MemoryMappedViewStream`. While memory mapping is effective for certain types of IPC and small files, for massive files it forces the OS to map the file into the filesystem cache, causing significant RAM overhead (working set bloat) and potential system stuttering. Moreover, `MemoryMappedViewStream.WriteAsync` does not support true asynchronous I/O natively, instead delegating to a `Task.Run` wrapper that incurs unnecessary thread pool scheduling overhead.

## Objective
Replace `MemoryMappedFile` usage with .NET 6+'s `System.IO.RandomAccess` API to achieve true overlapped asynchronous I/O and reduce memory pressure and thread pool overhead, resulting in higher throughput and stability during large downloads. 

## Approach

### 1. OS File Handle Management
In `OctaneEngineCore.Implementations.Engine.DownloadFile`:
- **.NET 6+:** Use `File.OpenHandle` to open a `SafeFileHandle` for the destination file with `FileOptions.Asynchronous`.
- **Legacy:** Retain `MemoryMappedFile.CreateFromFile` for `netstandard2.0` and `netstandard2.1` builds.
- Ensure the file handle is properly disposed of in a `finally` block or `using` statement.
- Pre-allocate the file size using `RandomAccess.SetLength(handle, length)` to ensure the disk has sufficient space before the parallel downloads begin.

### 2. Client Side Writing
In `OctaneClient.cs`:
- Introduce a new method `public void SetFileHandle(SafeFileHandle handle)` (wrapped in `#if NET6_0_OR_GREATER`).
- Update `RegularDownload`:
  - Calculate an absolute `fileOffset` initialized to `piece.start`.
  - Inside the `while` read loop, call `await RandomAccess.WriteAsync(_fileHandle, readBuffer.AsMemory(0, bytesRead), fileOffset, cancellationToken)`.
  - Increment `fileOffset += bytesRead`.
- Ensure fallback to `MemoryMappedViewStream` is seamlessly maintained for legacy targets.

### 3. Benchmarking Strategy
Before implementation, run the `BenchmarkOctaneProject` to establish a baseline. After applying the `RandomAccess` changes, rerun the benchmarks and record the difference in execution time and memory allocations to validate the effectiveness of the optimization.

## Error Handling and Edge Cases
- **Missing Handle Exception:** If a download starts without `_fileHandle` or `_mmf` being set, throw an `InvalidOperationException`.
- **Cross-Platform Compatibility:** The `RandomAccess` API translates perfectly to overlapped I/O on Windows (`WriteFile`) and pread/pwrite on Linux/macOS, so no OS-specific branches are needed beyond the .NET target framework (`#if NET6_0_OR_GREATER`).

## Testing Strategy
- Leverage existing integration tests and unit tests within the `OctaneTestProject` to ensure piece downloads complete identically and binary data matches expectations.
- Run local benchmark suites to verify throughput gains.
