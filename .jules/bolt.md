## 2025-05-14 — [Single Point Optimization]
**Learning:** Broad performance sweeps across multiple components without individual benchmarks can lead to potential regressions and maintainability issues. Specifically, reducing socket buffers from 1MB to 64KB might save memory but risks throughput degradation in a high-concurrency downloader.
**Action:** Focus on one high-impact, low-risk bottleneck (like system call overhead in network measurement) supported by evidence.

## 2025-05-14 — [Sandbox Network Restrictions]
**Learning:** The .NET 8.0 sandbox environment throws PlatformNotSupportedException during custom socket ConnectCallback logic in OctaneHttpClientPool, while .NET 10.0 handles it correctly. This makes net8.0 tests unreliable for verifying socket-level performance changes.
**Action:** Prioritize net10.0 for behavioral verification of networking logic in the current environment.

## 2025-05-14 — [TaskCompletionSource and Blocking Continuations]
**Learning:** When using TaskCompletionSource for non-blocking waits in IScheduler-based components like ThrottleStream, continuations can execute on the scheduler's blocking thread if not configured with TaskCreationOptions.RunContinuationsAsynchronously.
**Action:** Always use TaskCreationOptions.RunContinuationsAsynchronously for TCS in throttling logic.

## 2025-05-14 — [HttpClient Shared Instance and Response Disposal]
**Learning:** Reusing HttpClient instances prevents socket exhaustion, but failing to dispose of HttpResponseMessage (especially with ResponseHeadersRead) can still tie up connections in the pool.
**Action:** Ensure 'using var response' is used even when using a static shared HttpClient.
