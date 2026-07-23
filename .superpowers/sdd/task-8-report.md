# Task 8 Report: CommandBuffer rent + Playback modes

## Summary

- Added `CommandBufferFlag`, `ICommandBuffer`, pooled `CommandBuffer`, and `World.RentCommandBuffer`.
- Added CommandBuffer dispose-mode tests and deferred dense creation after query enumeration.
- Playback applies deferred operations in recording order through existing `Entity` immediate APIs.

## TDD Evidence

### Red

Command:

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test --filter FullyQualifiedName~CommandBuffer_ --verbosity normal
```

Result:

- Failed as expected before implementation.
- Compile errors: `World` missing `RentCommandBuffer`; `CommandBufferFlag` missing.
- Exit code: 1.

### Green

Command:

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test --filter FullyQualifiedName~CommandBuffer_ --verbosity normal
```

Result:

- Exit code: 0.

Concise verification:

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test --no-build --filter FullyQualifiedName~CommandBuffer_ --verbosity normal
```

Result:

- 4/4 passed:
  - `CommandBuffer_Default_AutoPlaybackOnDispose`
  - `CommandBuffer_Discard_DropsPending`
  - `CommandBuffer_MustManual_ThrowsIfDisposeWithoutPlayback`
  - `CommandBuffer_DeferDenseCreate_DuringQuery_ThenPlayback`

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test --no-build --filter FullyQualifiedName~EntityQuery --verbosity normal
```

Result:

- 6/6 passed.

## Full Suite

Command:

```bash
PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" dotnet test --verbosity normal
```

Result:

- Exit code: 1.
- 316/325 passed, 9 failed.
- Failures are in pre-existing `ComponentManagerTestUnit` / `IntegrationTestUnit` dense `ComponentStore` assumptions outside the Task 8 change set.
- Representative isolated failure:
  - `ComponentManager_ComponentStore_CapacityExpansionWorks` still fails alone with `Expected: greater than 100 But was: 100`.

## Files Changed

- `ECS/Defines/CommandBufferFlag.cs`
- `ECS/Defines/ICommandBuffer.cs`
- `ECS/Managers/CommandBuffer.cs`
- `ECS/World.cs`
- `Test/CommandBufferTestUnit.cs`
- `Test/EntityQueryTestUnit.cs`
