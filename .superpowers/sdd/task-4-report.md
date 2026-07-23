# Task 4 Report: EntityGraph location replaces RwComponents

## RED

- Added `EntityGraph_Pool_ResetClearsLocation`.
- Ran `dotnet test --filter FullyQualifiedName~EntityGraphTestUnit --verbosity normal`.
- Expected RED observed: test compile failed because `EntityGraph` did not yet define `ArchetypeId` or `Row`.

## GREEN

- `dotnet test --filter FullyQualifiedName~EntityGraphTestUnit --verbosity normal`: PASS.
- `dotnet build`: PASS, 0 warnings, 0 errors.
- `dotnet test --filter "FullyQualifiedName~EntityManagerTestUnit|FullyQualifiedName~EntityTestUnit|FullyQualifiedName~EntityMatcherTestUnit|FullyQualifiedName~EntityCollectorTestUnit" --verbosity normal`: PASS, 140/140.
- `dotnet test --verbosity normal`: PASS, 308/308.

## Failing tests

- None.

## Interim composition index

- Removed `RwComponents` and component accessor methods from `EntityGraph`.
- Added `EntityGraph.ArchetypeId` and `EntityGraph.Row`; pool reset clears them to `0` and `-1`.
- Added `EntityManager.m_entityComponents`, a private `Dictionary<ulong, List<IComponentRefCore>>` synchronized by existing component create/remove events.
- `Entity`, `World.Query`, and `EntityMatchManager` now read component membership through `EntityManager` internal accessors until Task 5+ chunk routing becomes the source of truth.

## Scope guard

- Did not implement archetype chunk routing or move entities into chunks.
