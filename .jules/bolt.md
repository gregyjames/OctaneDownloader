## 2025-05-14 — [Single Point Optimization]
**Learning:** Broad performance sweeps across multiple components without individual benchmarks can lead to potential regressions and maintainability issues. Specifically, reducing socket buffers from 1MB to 64KB might save memory but risks throughput degradation in a high-concurrency downloader.
**Action:** Focus on one high-impact, low-risk bottleneck (like system call overhead in network measurement) supported by evidence.

## 2025-05-14 — [Sandbox Network Restrictions]
**Learning:** The .NET 8.0 sandbox environment throws PlatformNotSupportedException during custom socket ConnectCallback logic in OctaneHttpClientPool, while .NET 10.0 handles it correctly. This makes net8.0 tests unreliable for verifying socket-level performance changes.
**Action:** Prioritize net10.0 for behavioral verification of networking logic in the current environment.

## 2025-05-14 — [Async Throttling and Thread Starvation]
**Learning:** Using synchronous blocking calls (like `WaitOne` on an `AutoResetEvent`) for throttling inside `ReadAsync` or `WriteAsync` can lead to thread pool starvation, especially in high-concurrency scenarios like parallel file downloads. This effectively negates the benefits of asynchronous I/O.
**Action:** Always provide and use a true asynchronous `ThrottleAsync` path using `await Task.Delay` to keep threads free for other work.
