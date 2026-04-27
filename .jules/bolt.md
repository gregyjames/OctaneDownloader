## 2025-05-14 — [Single Point Optimization]
**Learning:** Broad performance sweeps across multiple components without individual benchmarks can lead to potential regressions and maintainability issues. Specifically, reducing socket buffers from 1MB to 64KB might save memory but risks throughput degradation in a high-concurrency downloader.
**Action:** Focus on one high-impact, low-risk bottleneck (like system call overhead in network measurement) supported by evidence.

## 2025-05-14 — [Sandbox Network Restrictions]
**Learning:** The .NET 8.0 sandbox environment throws PlatformNotSupportedException during custom socket ConnectCallback logic in OctaneHttpClientPool, while .NET 10.0 handles it correctly. This makes net8.0 tests unreliable for verifying socket-level performance changes.
**Action:** Prioritize net10.0 for behavioral verification of networking logic in the current environment.

## 2025-05-14 — [Async Throttling & Stream Draining]
**Learning:** Replacing manual read loops with `CopyToAsync(Stream.Null)` simplifies code but can regress throughput if the original loop used a custom large buffer (e.g., 1MB). Always specify the buffer size in `CopyToAsync`. Additionally, throttling logic in high-concurrency downloaders MUST use `Task.Delay` to avoid thread pool exhaustion.
**Action:** Use `stream.CopyToAsync(Stream.Null, bufferSize)` and provide fully async paths for all throttled I/O.
