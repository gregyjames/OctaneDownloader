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

## 2026-06-14 — [HttpClient Pooling and Stream Copy Optimization]
**Learning:** In static utility contexts where DI is unavailable, reusing `HttpClient` via `private static readonly` fields is critical to prevent socket exhaustion and reduce handshake overhead. Additionally, using `stream.CopyToAsync(Stream.Null)` with an explicit buffer size is the most efficient way to discard data in .NET, outperforming manual read loops.
**Action:** Prefer `SharedClient` and `Stream.Null` for network analysis and data draining tasks.
