// All catalog tests share one SQL Server test database, isolating themselves with a rolled-back
// transaction per test. Disabling assembly-wide parallelism keeps that shared database serialized,
// avoiding lock contention / deadlocks between concurrently-running test classes.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
