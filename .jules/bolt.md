## 2025-05-14 — [Single Point Optimization]
**Learning:** Broad performance sweeps across multiple components without individual benchmarks can lead to potential regressions and maintainability issues. Specifically, reducing socket buffers from 1MB to 64KB might save memory but risks throughput degradation in a high-concurrency downloader.
**Action:** Focus on one high-impact, low-risk bottleneck (like system call overhead in network measurement) supported by evidence.

## 2025-05-14 — [Sandbox Network Restrictions]
**Learning:** The .NET 8.0 sandbox environment throws PlatformNotSupportedException during custom socket ConnectCallback logic in OctaneHttpClientPool, while .NET 10.0 handles it correctly. This makes net8.0 tests unreliable for verifying socket-level performance changes.
**Action:** Prioritize net10.0 for behavioral verification of networking logic in the current environment.

## 2025-05-14 — [Async Throttling Optimization]
**Learning:** Synchronous blocking in `ThrottleStream` during asynchronous operations caused significant thread pool utilization (Sync-over-Async). By implementing `ThrottleAsync` with `await Task.Delay` and overriding all `ReadAsync`/`WriteAsync` variants, thread pool usage for 200 concurrent throttled streams was reduced from ~40-50 threads to 1-3 threads.
**Action:** Ensure all asynchronous I/O paths in custom streams have corresponding non-blocking throttling/delay logic.
