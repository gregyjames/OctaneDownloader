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

## 2025-05-17 — [Hot-Path Pause Checks and ConfigureAwait]
**Learning:** In high-throughput streaming loops where pause tokens are checked on every buffer read/write, delegating or instantiating task objects when unpaused adds allocation and call stack overhead. Early-exiting on `!_tokenSource.IsPaused` avoids Task creation and volatile state checks. Additionally, missing `.ConfigureAwait(false)` on hot stream operations causes unnecessary context synchronization across thread pool continuations.
**Action:** Always short-circuit unpaused `PauseToken` checks and ensure `.ConfigureAwait(false)` is used on every await statement in library streaming pipelines.

## 2025-05-18 — [MemoryMappedViewStream Writing and Uri Reuse]
**Learning:** Calling `await stream.WriteAsync(...)` on a `MemoryMappedViewStream` inside high-frequency download loops creates unnecessary ValueTask and Task state machine machinery for an operation that is a synchronous memory copy into unmanaged memory pointers. Furthermore, instantiating `new Uri(url)` and evaluating empty collections like `request.Headers ?? []` per piece creates repeated heap allocations. Reusing pre-parsed `Uri` objects, passing `null` directly, and using synchronous `stream.Write(...)` on memory views reduces managed allocations per download by over 99.8%.
**Action:** Use synchronous `stream.Write(...)` for memory-backed stream implementations like `MemoryMappedViewStream` and pre-parse/reuse objects before entering parallel piece processing loops.
