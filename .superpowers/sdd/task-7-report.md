# Task 7 Report: EntityQuery streaming + read locks + World collection wrappers

## Status

Implemented `EntityQuery` as a streaming `IEnumerable<Entity>` with a disposable enumerator that read-locks candidate archetypes for the lifetime of enumeration. `World.Query(IEntityMatcher)` now returns `EntityQuery`, and the existing collection overloads stream through it.

## Changes

- Added `ECS/EntityQuery.cs`.
- Added `World.Query(IEntityMatcher)` and rewired `World.Query(..., ICollection<ulong>)` / `World.Query(..., ICollection<Entity>)` to foreach over the streaming query.
- Exposed archetype iteration internally through `ComponentManager.ArchetypeRegistry` and `ArchetypeRegistry.Archetypes`.
- Added `EntityMatcher.CouldMatchDenseSignature(...)` for dense candidate selection before sparse proxy filtering.
- Added `Test/EntityQueryTestUnit.cs` for streaming, read-lock, disposal, and sparse-only query behavior.

## TDD Evidence

### RED

Command:

```bash
PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet dotnet test --filter FullyQualifiedName~EntityQuery_ --verbosity normal
```

Result: failed at compile time as expected because `World.Query(IEntityMatcher)` did not exist yet.

Key errors:

```text
error CS1501: No overload for method 'Query' takes 1 arguments
error CS1579: foreach statement cannot operate on variables of type 'int'
```

### GREEN

Command:

```bash
PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet dotnet test --filter "FullyQualifiedName~EntityQuery_|FullyQualifiedName~World_Query" --verbosity minimal
```

Result:

```text
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14
```

### Full Suite

Command:

```bash
PATH=$HOME/.dotnet:$PATH DOTNET_ROOT=$HOME/.dotnet dotnet test --no-build --verbosity minimal
```

Result:

```text
Failed!  - Failed: 9, Passed: 312, Skipped: 0, Total: 321
```

The failures are component-store expectation tests (`ComponentManagerTestUnit` and one integration test) that expect dense components to allocate in `ComponentStore`; current branch behavior routes dense components through archetype chunks. Task 7 changes do not modify component allocation/routing.

## Concerns

- Full suite is not green due to the 9 existing dense-vs-store expectation failures listed above.
- Focused Task 7 query coverage is green.
