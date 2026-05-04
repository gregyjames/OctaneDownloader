## 2025-05-14 — [Single Point Optimization]
**Learning:** Broad performance sweeps across multiple components without individual benchmarks can lead to potential regressions and maintainability issues. Specifically, reducing socket buffers from 1MB to 64KB might save memory but risks throughput degradation in a high-concurrency downloader.
**Action:** Focus on one high-impact, low-risk bottleneck (like system call overhead in network measurement) supported by evidence.

## 2025-05-14 — [Sandbox Network Restrictions]
**Learning:** The .NET 8.0 sandbox environment throws PlatformNotSupportedException during custom socket ConnectCallback logic in OctaneHttpClientPool, while .NET 10.0 handles it correctly. This makes net8.0 tests unreliable for verifying socket-level performance changes.
**Action:** Prioritize net10.0 for behavioral verification of networking logic in the current environment.

## 2026-05-04 — [Async Throttling in Custom Streams]
**Learning:** Custom Stream implementations that implement throttling via thread-blocking waits (e.g., `WaitHandle.WaitOne()` or `Thread.Sleep`) in asynchronous paths like `ReadAsync` or `WriteAsync` cause "sync-over-async" bottlenecks, leading to thread pool starvation.
**Action:** Always provide an asynchronous `ThrottleAsync` path using `Task.Delay` or async-compatible schedulers, and ensure all `ReadAsync` and `WriteAsync` overloads (including `Memory<T>` and legacy `byte[]`) are overridden to use it.
