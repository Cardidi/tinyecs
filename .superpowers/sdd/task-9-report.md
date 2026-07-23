# Task 9 Report: Collector Hybrid Strategy C

## Summary

- Implemented matcher-level dense archetype id caches for collectors.
- Routed dense structural changes through source/destination dense signature set difference, using sparse `Matches` only when the dense transition can enter a matching archetype.
- Routed sparse structural changes through notification relevance and current `Matches` with proxy.
- Kept revision changes on notification state only; no collector archetype read locks were added.
- Preserved `Flush` and collector flag semantics.

## Tests Added

- `EntityCollector_DenseSecondInstance_StaysMatchedAfterMigrate`
- `EntityCollector_SparseAdd_PublishesOnlyAfterFlush`
- `EntityCollector_SparseOnlyMatcher_IgnoresIrrelevantDenseMigrate`

## TDD Evidence

RED:

```text
dotnet test --no-build --filter "FullyQualifiedName~EntityCollector_SparseOnlyMatcher_IgnoresIrrelevantDenseMigrate" --verbosity minimal
Failed EntityCollector_SparseOnlyMatcher_IgnoresIrrelevantDenseMigrate
Expected: 0
But was:  1
```

GREEN:

```text
dotnet test --filter "FullyQualifiedName~EntityCollector_DenseSecondInstance_StaysMatchedAfterMigrate|FullyQualifiedName~EntityCollector_SparseAdd_PublishesOnlyAfterFlush|FullyQualifiedName~EntityCollector_SparseOnlyMatcher_IgnoresIrrelevantDenseMigrate" --verbosity normal
Test Run Successful. Total tests: 3 Passed: 3
```

Collector suite:

```text
dotnet test --filter "FullyQualifiedName~EntityCollector|FullyQualifiedName~Collector_" --verbosity normal
Test Run Successful. Total tests: 68 Passed: 68
```

Full suite:

```text
dotnet test --verbosity normal
Test Run Failed. Total tests: 328 Passed: 319 Failed: 9
```

The full-suite failures reproduce in non-collector `ComponentManager_ComponentStore*` and lifecycle tests that inspect dense components through `ComponentStore`; this task changed only `ECS/Managers/EntityMatchManager.cs` and `Test/EntityCollectorTestUnit.cs`.
