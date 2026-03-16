using NUnit.Framework;

// Parallelize at the Fixture level (each test class runs in parallel)
// Since most tests involve file I/O and network, Fixtures is a safe starting point.
[assembly: Parallelizable(ParallelScope.Fixtures)]

// Level of parallelism (default is processor count)
[assembly: LevelOfParallelism(8)]
