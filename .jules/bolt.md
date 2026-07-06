## 2025-05-14 — [Single Point Optimization]
**Learning:** Broad performance sweeps across multiple components without individual benchmarks can lead to potential regressions and maintainability issues. Specifically, reducing socket buffers from 1MB to 64KB might save memory but risks throughput degradation in a high-concurrency downloader.
**Action:** Focus on one high-impact, low-risk bottleneck (like system call overhead in network measurement) supported by evidence.

## 2025-05-14 — [Sandbox Network Restrictions]
**Learning:** The .NET 8.0 sandbox environment throws PlatformNotSupportedException during custom socket ConnectCallback logic in OctaneHttpClientPool, while .NET 10.0 handles it correctly. This makes net8.0 tests unreliable for verifying socket-level performance changes.
**Action:** Prioritize net10.0 for behavioral verification of networking logic in the current environment.

## 2025-05-15 — [Async-over-Sync in Streams]
**Learning:** Overriding `ReadAsync` and `WriteAsync` in a custom `Stream` is insufficient if the implementation calls a blocking method (like `WaitOne` or `Thread.Sleep`). This causes thread pool starvation during throttled operations. Using `await Task.Delay` or `TaskCompletionSource` with `IScheduler` maintains both performance and testability.
**Action:** Always provide truly asynchronous paths in custom `Stream` implementations, especially for high-latency operations like throttling.

## 2025-05-16 — [Allocation-Free Rendering with ZLinq]
**Learning:** High-frequency UI rendering (like a CLI progress bar) should avoid standard LINQ operations (.Where, .Select, .ToList) and anonymous objects to minimize GC pressure. While manual loops are effective, libraries like `ZLinq` provide allocation-free LINQ-like extensions using value-typed enumerators, allowing for both readability and performance.
**Action:** Use `ZLinq` or manual loops to avoid heap allocations in hot paths like render ticks.

## 2026-07-05 — [NetworkAnalyzer and HttpClient Reuse]
**Learning:** Reusing HttpClient via a static field prevents socket exhaustion and handshake overhead in library utility methods. Using Stream.CopyToAsync(Stream.Null, 1024 * 1024) is the most efficient way to discard data in .NET, reducing system call overhead compared to manual loops. Correcting integer division to floating-point in PrettySize ensures accurate reporting while maintaining performance with ZString.
**Action:** For internal measurements and utility calls, always prefer a shared HttpClient instance and use Stream.Null for data discard paths.

## 2026-07-06 — [Refined NetworkAnalyzer and HttpClient Optimization]
**Learning:** Initial optimizations lacked .ConfigureAwait(false) and CancellationToken support, which are critical for library performance and robustness. Reusing HttpClient via a static field is enhanced by configuring SocketsHttpHandler.PooledConnectionLifetime to ensure DNS updates. Restoring the full InternalsVisibleTo with PublicKey is necessary for projects that enforce assembly signing.
**Action:** Always include .ConfigureAwait(false) and CancellationToken in library networking calls. Use conditional compilation for framework-specific SocketsHttpHandler optimizations.

## 2026-07-06 — [Comprehensive Diagnostic and Resource Optimization]
**Learning:** Extending CancellationToken support to IPingService and using modern SendPingAsync overloads (.NET 7+) ensures all diagnostic paths are robust. Reusing SharedClient in HttpDownloader further eliminates redundant connection overhead. Conditional compilation allows for using modern APIs while maintaining multi-targeting support.
**Action:** Always verify library interfaces for CancellationToken consistency. Use #if NET7_0_OR_GREATER for modern Ping APIs.
