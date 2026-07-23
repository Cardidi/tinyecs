# Archetype Chunk + SparseSetProxy Storage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate dense `IComponent` storage to Archetype Chunks with `SparseSetProxy`, keep `ISparseComponent` on `ComponentStore`, and ship streaming `EntityQuery` + rented `CommandBuffer` without renaming `ComponentStore`.

**Architecture:** Dense components live in SoA `ComponentChunk`s keyed by multiplicity-aware archetype signatures; sparse components stay in existing `ComponentStore` with per-row `SparseSetProxy` handles. `EntityGraph` stores archetype location (no `RwComponents`). `EntityQuery` streams matching entities under archetype read locks; collectors use hybrid dense archetype indexing + sparse/RW notifications. Structural dense changes while locked go through a rented `ICommandBuffer`.

**Tech Stack:** Pure C# CoreECS (`net8.0` + `netstandard2.1`, `LangVersion 9`), NUnit tests (`Test/`), Conventional Commits.

**Spec:** `docs/superpowers/specs/2026-07-23-archetype-chunk-sparse-storage-design.md`

## Global Constraints

- Preserve Entity façade: `CreateComponent` / `Get*` / `Destroy*` / `ComponentRef` RO·RW call sites where possible; additive APIs only.
- Multi-instance same type allowed on both dense and sparse backends; matcher presence = count ≥ 1.
- Dense archetype key = multiset `(Type × Count)`; sparse does **not** change archetype.
- `ISparseComponent<T> : IComponent<T>`; non-sparse `IComponent` → Chunk; sparse → `ComponentStore`.
- Immediate dense migrate when not read-locked; under lock → throw (use CommandBuffer).
- CommandBuffer: rent `IDisposable`; `CommandBufferFlag` modes `Default`=`AutoPlaybackOnDispose`, `AutoPlaybackOnDispose`, `DiscardPendingOnDispose`, `MustManualPlaybackOnDispose`.
- `EntityQuery`: primary `IEnumerable<Entity>` with disposable enumerator holding read locks; fill-`ICollection` overloads are wrappers.
- Collector does **not** take archetype read locks; hybrid strategy C.
- Do **not** rename `ComponentStore` → `ComponentSparse`.
- Do **not** unify csproj Nullable/ImplicitUsings differences.
- English identifiers/XML docs; `m_` private fields; throw on illegal states (no silent failure).
- `dotnet test` after each task; accept temporary failures only in layout-coupled `ComponentManagerTestUnit` asserts called out in the spec (document in commit body if needed).
- Commit format: Conventional Commits (`feat`/`fix`/`refactor`/`test`/`doc` + scope).

---

## File structure (create / modify)

| Path | Role |
|------|------|
| `ECS/Defines/ISparseComponent.cs` | Sparse marker interface |
| `ECS/Defines/CommandBufferFlag.cs` | Rent mode enum |
| `ECS/Defines/ICommandBuffer.cs` | Defer + Playback contract |
| `ECS/Defines/IEntityMatcher.cs` | Add `Matches`; deprecate list `ComponentFilter` as primary |
| `ECS/Defines/IComponent.cs` | Unchanged contract (sparse inherits) |
| `ECS/Managers/ArchetypeSignature.cs` | Dense `(Type, Count)` key + equality/hash |
| `ECS/Managers/Archetype.cs` | Chunk list, read-lock count, column layout meta |
| `ECS/Managers/ArchetypeRegistry.cs` | Signature → Archetype; empty/proxy-only archetype |
| `ECS/Managers/SparseSetProxy.cs` | Per-row handle list |
| `ECS/Managers/ComponentChunk.cs` | SoA storage + row allocate/remove/migrate helpers |
| `ECS/Managers/CommandBuffer.cs` | Pooled `ICommandBuffer` implementation |
| `ECS/Managers/ComponentManager.cs` | Route dense/sparse; own registry/locks; create/destroy |
| `ECS/Managers/EntityManager.cs` | Location updates; destroy without `RwComponents` |
| `ECS/Managers/EntityMatchManager.cs` | `Matches` + hybrid dense index |
| `ECS/EntityGraph.cs` | Replace `RwComponents` with `ArchetypeId`/`Row` |
| `ECS/Entity.cs` | Accessors via chunk/proxy; lock checks |
| `ECS/EntityQuery.cs` | Streaming enumerable + locks |
| `ECS/EntityMatcher.cs` | Dense/sparse condition split + `Matches` |
| `ECS/EntityMatcherExtension.cs` | Query → `EntityQuery` |
| `ECS/World.cs` | `Query` enumerable + rent buffer; wrap collections |
| `Test/SparseComponentTestUnit.cs` | Routing + mixed OfAll |
| `Test/ArchetypeChunkTestUnit.cs` | Multiplicity migrate / proxy-only |
| `Test/CommandBufferTestUnit.cs` | Flag modes |
| `Test/EntityQueryTestUnit.cs` | Streaming + lock throw |
| `Test/EntityGraphTestUnit.cs` | Rewrite off `RwComponents` |
| `Test/EntityManagerTestUnit.cs` | Drop `RwComponents` coupling |
| `Test/ComponentManagerTestUnit.cs` | Leave dense layout asserts failing OR quarantine (per spec non-goal) |

---

### Task 1: `ISparseComponent` + sparse detection helper

**Files:**
- Create: `ECS/Defines/ISparseComponent.cs`
- Create: `ECS/Managers/ComponentStorageKind.cs` (internal helper)
- Test: `Test/SparseComponentTestUnit.cs`

**Interfaces:**
- Consumes: `IComponent<T>` in `ECS/Defines/IComponent.cs`
- Produces:
  - `public interface ISparseComponent<TComponent> : IComponent<TComponent> where TComponent : struct, ISparseComponent<TComponent>`
  - `internal static class ComponentStorageKind` with `public static bool IsSparse(Type componentType)` and `public static bool IsSparse<T>() where T : struct, IComponent<T>`

- [ ] **Step 1: Write the failing test**

```csharp
// Test/SparseComponentTestUnit.cs
using CoreECS.Defines;
using CoreECS.Managers;
using NUnit.Framework;

namespace CoreECS.Test
{
    public struct DenseProbe : IComponent<DenseProbe> { }
    public struct SparseProbe : ISparseComponent<SparseProbe> { }

    public class SparseComponentTestUnit
    {
        [Test]
        public void ComponentStorageKind_DetectsSparseInterface()
        {
            Assert.IsFalse(ComponentStorageKind.IsSparse<DenseProbe>());
            Assert.IsTrue(ComponentStorageKind.IsSparse<SparseProbe>());
            Assert.IsTrue(ComponentStorageKind.IsSparse(typeof(SparseProbe)));
        }
    }
}
```

If `ComponentStorageKind` is `internal`, keep `InternalsVisibleTo` (already on ECS → Test).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~ComponentStorageKind_DetectsSparseInterface --verbosity normal`  
Expected: FAIL (type/interface missing)

- [ ] **Step 3: Write minimal implementation**

```csharp
// ECS/Defines/ISparseComponent.cs
namespace CoreECS.Defines
{
    /// <summary>
    /// Marker for components stored in the sparse ComponentStore path.
    /// </summary>
    public interface ISparseComponent<TComponent> : IComponent<TComponent>
        where TComponent : struct, ISparseComponent<TComponent>
    {
    }
}
```

```csharp
// ECS/Managers/ComponentStorageKind.cs
using System;
using CoreECS.Defines;

namespace CoreECS.Managers
{
    internal static class ComponentStorageKind
    {
        public static bool IsSparse<T>() where T : struct, IComponent<T>
            => typeof(ISparseComponent<T>).IsAssignableFrom(typeof(T));

        public static bool IsSparse(Type componentType)
        {
            if (componentType == null || !componentType.IsValueType) return false;
            var sparseOpen = typeof(ISparseComponent<>);
            var closed = sparseOpen.MakeGenericType(componentType);
            return closed.IsAssignableFrom(componentType);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~ComponentStorageKind_DetectsSparseInterface --verbosity normal`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ECS/Defines/ISparseComponent.cs ECS/Managers/ComponentStorageKind.cs Test/SparseComponentTestUnit.cs
git commit -m "feat(core): add ISparseComponent and storage-kind detection"
```

---

### Task 2: Archetype signature, registry, SparseSetProxy, empty archetype

**Files:**
- Create: `ECS/Managers/ArchetypeSignature.cs`
- Create: `ECS/Managers/SparseSetProxy.cs`
- Create: `ECS/Managers/Archetype.cs`
- Create: `ECS/Managers/ArchetypeRegistry.cs`
- Test: `Test/ArchetypeChunkTestUnit.cs` (signature equality section)

**Interfaces:**
- Consumes: `ComponentStorageKind`
- Produces:
  - `internal readonly struct ArchetypeSignature` with `Equals`/`GetHashCode` over sorted `(Type, int Count)` entries; empty = proxy-only
  - `internal sealed class SparseSetProxy` with `List<IComponentRefCore> Handles` (or pooled list), `Add`/`Remove`/`Has(Type)`/`Clear`
  - `internal sealed class Archetype` with `ArchetypeSignature Signature`, `int Id`, `int ReadLockCount`, `void AddReadLock()`, `void RemoveReadLock()`, `bool IsReadLocked => ReadLockCount > 0`
  - `internal sealed class ArchetypeRegistry` with `Archetype GetOrCreate(ArchetypeSignature signature)`, `Archetype Empty` (proxy-only), `Archetype Get(int id)`

- [ ] **Step 1: Write the failing tests**

```csharp
[Test]
public void ArchetypeSignature_Equals_ByTypeAndCount()
{
    var a = ArchetypeSignature.From((typeof(DenseProbe), 1), (typeof(int), 2)); // use real dense component types
    var b = ArchetypeSignature.From((typeof(DenseProbe), 1), (typeof(int), 2));
    var c = ArchetypeSignature.From((typeof(DenseProbe), 2));
    Assert.AreEqual(a, b);
    Assert.AreNotEqual(a, c);
}

[Test]
public void ArchetypeRegistry_Empty_IsProxyOnly()
{
    var registry = new ArchetypeRegistry();
    Assert.AreEqual(0, registry.Empty.Signature.Entries.Count);
    Assert.AreSame(registry.Empty, registry.GetOrCreate(ArchetypeSignature.Empty));
}
```

Use project-local test component structs already in the test file (e.g. `DenseProbe`), not `int`. Adjust `From` API to only accept component types used in tests.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter FullyQualifiedName~ArchetypeSignature|FullyQualifiedName~ArchetypeRegistry_Empty --verbosity normal`  
Expected: FAIL

- [ ] **Step 3: Implement signature, proxy, archetype, registry**

Implement:

```csharp
internal readonly struct ArchetypeEntry
{
    public readonly Type Type;
    public readonly int Count;
    public ArchetypeEntry(Type type, int count) { Type = type; Count = count; }
}

internal readonly struct ArchetypeSignature : IEquatable<ArchetypeSignature>
{
    public static ArchetypeSignature Empty { get; } // zero entries
    public IReadOnlyList<ArchetypeEntry> Entries { get; }
    public static ArchetypeSignature From(params ArchetypeEntry[] entries); // sort by Type.FullName then Count; reject Count < 1
    // Equals/GetHashCode over ordered entries
}

internal sealed class SparseSetProxy
{
    public List<IComponentRefCore> Handles { get; } = new List<IComponentRefCore>();
    public void Add(IComponentRefCore core) { Handles.Add(core); }
    public bool Remove(IComponentRefCore core) { return Handles.Remove(core); }
    public bool Has(Type t) { /* scan IsT / GetT */ }
    public void Clear() { Handles.Clear(); }
}

internal sealed class Archetype
{
    public int Id { get; }
    public ArchetypeSignature Signature { get; }
    public int ReadLockCount { get; private set; }
    public void AddReadLock() { ReadLockCount++; }
    public void RemoveReadLock()
    {
        if (ReadLockCount <= 0) throw new InvalidOperationException("Archetype read lock underflow.");
        ReadLockCount--;
    }
    public bool IsReadLocked => ReadLockCount > 0;
    // Chunk list added in Task 3
}

internal sealed class ArchetypeRegistry
{
    public Archetype Empty { get; }
    public Archetype GetOrCreate(ArchetypeSignature signature);
    public Archetype Get(int id);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter FullyQualifiedName~ArchetypeSignature|FullyQualifiedName~ArchetypeRegistry_Empty --verbosity normal`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ECS/Managers/ArchetypeSignature.cs ECS/Managers/SparseSetProxy.cs ECS/Managers/Archetype.cs ECS/Managers/ArchetypeRegistry.cs Test/ArchetypeChunkTestUnit.cs
git commit -m "feat(core): add archetype signature, registry, and SparseSetProxy"
```

---

### Task 3: `ComponentChunk` dense columns + row lifecycle

**Files:**
- Create: `ECS/Managers/ComponentChunk.cs`
- Modify: `ECS/Managers/Archetype.cs` (own chunks)
- Test: `Test/ArchetypeChunkTestUnit.cs`

**Interfaces:**
- Consumes: `Archetype`, `ArchetypeSignature`, `SparseSetProxy`
- Produces:
  - `internal sealed class ComponentChunk` with `int Capacity` (default **64**), `int Count`, `ulong[] EntityIds`, per-type column storage, `SparseSetProxy[] Proxies`
  - `int AddRow(ulong entityId)` → row index
  - `void RemoveRowSwapBack(int row)` (swap-remove; caller relocates refs)
  - Column accessors for dense `T` instance index `0..Count-1` for type T
  - `Archetype` methods: `ComponentChunk GetChunkWithSpace()`, `IReadOnlyList<ComponentChunk> Chunks`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void ComponentChunk_AddAndRemoveRow_UpdatesCountAndProxy()
{
    var registry = new ArchetypeRegistry();
    var sig = ArchetypeSignature.From(new ArchetypeEntry(typeof(DenseProbe), 1));
    var arch = registry.GetOrCreate(sig);
    var chunk = arch.GetChunkWithSpace();
    var row = chunk.AddRow(42UL);
    Assert.AreEqual(42UL, chunk.EntityIds[row]);
    Assert.IsNotNull(chunk.Proxies[row]);
    Assert.AreEqual(0, chunk.Proxies[row].Handles.Count);
    chunk.RemoveRowSwapBack(row);
    Assert.AreEqual(0, chunk.Count);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~ComponentChunk_AddAndRemoveRow --verbosity normal`  
Expected: FAIL

- [ ] **Step 3: Implement chunk storage**

Use SoA: for each `(Type, Count)` in signature, allocate `Count` arrays (or one slab). For `DenseProbe×1`, one `DenseProbe[]` of length `Capacity`. Store `ComponentRefCore` per dense instance in parallel arrays for relocate. Keep implementation in `ComponentChunk` / helper `DenseColumn` without exposing store layout to Entity API.

Default capacity constant:

```csharp
internal const int DefaultChunkCapacity = 64;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~ComponentChunk_AddAndRemoveRow --verbosity normal`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ECS/Managers/ComponentChunk.cs ECS/Managers/Archetype.cs Test/ArchetypeChunkTestUnit.cs
git commit -m "feat(core): add ComponentChunk SoA row allocate and swap-remove"
```

---

### Task 4: EntityGraph location replaces `RwComponents`

**Files:**
- Modify: `ECS/EntityGraph.cs`
- Modify: `Test/EntityGraphTestUnit.cs` (remove/rewrite `RwComponents` tests; cover location fields)
- Modify: `Test/EntityManagerTestUnit.cs` (any `RwComponents` asserts)

**Interfaces:**
- Consumes: archetype id/row from registry
- Produces on `EntityGraph`:
  - `public int ArchetypeId { get; set; }`
  - `public int Row { get; set; }`
  - Remove `RwComponents` property and all methods that iterate it — move Get/Has/Count implementation later to Entity/ComponentManager (Task 5–6). For this task, keep graph as location + mask/generation/wishDestroy + pool reset clearing location to empty archetype sentinel (`ArchetypeId = emptyId`, `Row = -1`).

- [ ] **Step 1: Write failing tests for location reset**

```csharp
[Test]
public void EntityGraph_Pool_ResetClearsLocation()
{
    var graph = EntityGraph.Pool.Get();
    graph.ArchetypeId = 3;
    graph.Row = 7;
    EntityGraph.Pool.Release(graph);
    var again = EntityGraph.Pool.Get();
    Assert.AreEqual(0, again.ArchetypeId); // empty archetype id from registry convention, or -1 if chosen — match implementation
    Assert.AreEqual(-1, again.Row);
}
```

Rewrite/remove tests that mutate `RwComponents` directly (`EntityGraph_RwComponentsProperty_ManipulationWorks`, pool empty list asserts). Replace GetComponent tests that depended on manually stuffing `RwComponents` with integration tests in later tasks.

- [ ] **Step 2: Run to see compile/fail on `RwComponents` removals**

Run: `dotnet build`  
Expected: errors wherever `RwComponents` remains — fix call sites in EntityManager/World/MatchManager **minimally** with `#if false` stubs only if needed; prefer completing Task 5–7 in order. If build is too broken, land EntityGraph API change in the same commit as EntityManager stub that stops writing `RwComponents` and temporarily breaks match until Task 7 (document known red tests).

Practical approach for this task commit: remove `RwComponents`; update `EntityGraph` accessors to throw `NotImplementedException` or delegate to a static hook set by ComponentManager in Task 5. Prefer **delegating** to `IComponentAccess` injected later — simplest acceptable interim: keep Get* methods on EntityGraph but implement via `World`/manager lookup using ArchetypeId+Row once Task 5 lands; for Task 4 only change fields + pool reset + fix compile by deleting list-based loops and making Get/Has call into `ComponentManager` if already available, else leave methods throwing until Task 5–6 in the **same PR branch sequence** (do not leave master broken across pushes if CI runs — keep branch building).

**Build rule:** After this task, `dotnet build` must succeed. If EntityGraph.GetComponent still needed, implement thin forwarders that require a `ComponentManager` reference stored on the graph at create time, or move Get* entirely to `Entity` (Entity already has manager refs) and delete Get* from EntityGraph used by tests — update tests accordingly.

- [ ] **Step 3: Implement EntityGraph location fields; update tests**

```csharp
public int ArchetypeId { get; set; }
public int Row { get; set; } = -1;
// Reset():
ArchetypeId = 0; // empty
Row = -1;
WishDestroy = false;
// remove RwComponents.Clear()
```

- [ ] **Step 4: Run EntityGraph tests**

Run: `dotnet test --filter FullyQualifiedName~EntityGraphTestUnit --verbosity normal`  
Expected: PASS for updated tests

- [ ] **Step 5: Commit**

```bash
git add ECS/EntityGraph.cs Test/EntityGraphTestUnit.cs Test/EntityManagerTestUnit.cs
git commit -m "refactor(core): replace EntityGraph RwComponents with archetype location"
```

---

### Task 5: ComponentManager dense create/destroy migrate + sparse Store path

**Files:**
- Modify: `ECS/Managers/ComponentManager.cs`
- Modify: `ECS/Managers/EntityManager.cs`
- Modify: `ECS/Entity.cs`
- Test: `Test/ArchetypeChunkTestUnit.cs`, `Test/SparseComponentTestUnit.cs`, `Test/EntityTestUnit.cs` (run existing)

**Interfaces:**
- Consumes: `ArchetypeRegistry`, `ComponentChunk`, `ComponentStorageKind`, existing `ComponentStore<T>`
- Produces:
  - `CreateComponent<T>(ulong entityId)` routes sparse → Store + proxy handle; dense → migrate archetype Count+1, write column, return `IComponentRefCore`
  - `DestroyComponent` inverse
  - `bool TryBeginDenseStructuralChange(Archetype archetype)` / throw if `archetype.IsReadLocked`
  - Entity create places entity in **empty/proxy-only** archetype row
  - Events `OnComponentCreated/Removed/Changed` still fire on real mutation
  - `GetComponentStore<T>` remains for sparse (and may still exist for types historically tested — dense types no longer allocate Store)

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void DenseCreate_MigratesArchetypeByCount()
{
    var world = new World();
    var e = world.CreateEntity();
    e.CreateComponent<DenseProbe>();
    e.CreateComponent<DenseProbe>();
    Assert.AreEqual(2, e.GetComponentCount<DenseProbe>());
}

[Test]
public void SparseCreate_DoesNotRequireDenseColumns()
{
    var world = new World();
    var e = world.CreateEntity();
    e.CreateComponent<SparseProbe>();
    Assert.IsTrue(e.HasComponent<SparseProbe>());
    Assert.IsFalse(e.HasComponent<DenseProbe>());
}
```

- [ ] **Step 2: Run tests to verify they fail / wrong behavior**

Run: `dotnet test --filter FullyQualifiedName~DenseCreate_Migrates|FullyQualifiedName~SparseCreate_DoesNot --verbosity normal`  
Expected: FAIL until routing exists

- [ ] **Step 3: Implement routing and migration**

Algorithm for dense create:

1. Resolve entity graph → current archetype/row.
2. If current archetype `IsReadLocked` → throw `InvalidOperationException` with message requiring CommandBuffer.
3. Build new signature with Count+1 for `T`.
4. Allocate row in destination chunk; copy dense columns instance-by-instance; move `SparseSetProxy` handles list; `Relocate` dense cores; free old row (swap-back); update `EntityGraph.ArchetypeId/Row`.
5. Write new component value; `OnCreate`; raise created signal.

Sparse create:

1. `ComponentStore<T>.Fix` as today.
2. Append core to current row `Proxies`.
3. Raise created signal.
4. Do **not** change archetype.

Mirror for destroy (first-match for `DestroyComponent<T>()`, specific core for ref destroy).

Entity destroy: remove all sparse via Store; remove chunk row; clear location.

- [ ] **Step 4: Run Entity + new tests**

Run: `dotnet test --filter FullyQualifiedName~EntityTestUnit|FullyQualifiedName~DenseCreate|FullyQualifiedName~SparseCreate --verbosity normal`  
Expected: Entity semantics PASS; new tests PASS  
Note: `ComponentManagerTestUnit` layout asserts may FAIL — allowed per spec; do not “fix” by putting dense back on Store.

- [ ] **Step 5: Commit**

```bash
git add ECS/Managers/ComponentManager.cs ECS/Managers/EntityManager.cs ECS/Entity.cs Test/ArchetypeChunkTestUnit.cs Test/SparseComponentTestUnit.cs
git commit -m "feat(core): route dense components to chunks and sparse to ComponentStore"
```

---

### Task 6: Matcher `Matches` (dense signature + proxy)

**Files:**
- Modify: `ECS/Defines/IEntityMatcher.cs`
- Modify: `ECS/EntityMatcher.cs`
- Modify: `Test/EntityMatcherTestUnit.cs` (keep OfAll API tests; add mixed sparse if needed)
- Test: `Test/SparseComponentTestUnit.cs`

**Interfaces:**
- Consumes: `ArchetypeSignature`, `SparseSetProxy`
- Produces:
  - `bool Matches(ArchetypeSignature denseSignature, SparseSetProxy sparseProxy);`
  - Internal split of all/any/none into dense vs sparse type sets at build time or first use
  - Keep fluent `OfAll`/`OfAny`/`OfNone`; keep `IsRelevantComponent`
  - `ComponentFilter(IReadOnlyCollection<IComponentRefCore>)` marked `[Obsolete]` and implemented by scanning list for presence (compat) OR removed if no remaining callers — prefer obsolete wrapper used only by legacy tests if any

- [ ] **Step 1: Write failing test**

```csharp
[Test]
public void EntityMatcher_OfAll_MixedSparseAndDense_MatchesProxyAndSignature()
{
    var matcher = EntityMatcher.With().OfAll<DenseProbe>().OfAll<SparseProbe>();
    var sig = ArchetypeSignature.From(new ArchetypeEntry(typeof(DenseProbe), 1));
    var proxy = new SparseSetProxy();
    // add a mock/sparse ref core typed as SparseProbe into proxy after creating via world is easier:
    var world = new World();
    var e = world.CreateEntity();
    e.CreateComponent<DenseProbe>();
    e.CreateComponent<SparseProbe>();
    Assert.AreEqual(1, world.Query(matcher, new List<ulong>()));
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `dotnet test --filter FullyQualifiedName~OfAll_MixedSparseAndDense --verbosity normal`  
Expected: FAIL until Matches + Query wiring (if Query still list-based on RwComponents, finish Matches here and Task 7 for Query)

- [ ] **Step 3: Implement Matches**

```csharp
public bool Matches(ArchetypeSignature denseSignature, SparseSetProxy sparseProxy)
{
    // For each type in m_all: if sparse kind → proxy.Has; else denseSignature has Type with Count>=1
    // For m_any: at least one present
    // For m_none: none present
}
```

Update `IsRelevantComponent` unchanged logically.

- [ ] **Step 4: Run matcher tests**

Run: `dotnet test --filter FullyQualifiedName~EntityMatcherTestUnit|FullyQualifiedName~OfAll_MixedSparseAndDense --verbosity normal`  
Expected: PASS (Query assertion may wait for Task 7 — if so split test to call `matcher.Matches` directly with signature+proxy from internals)

Prefer also:

```csharp
Assert.IsTrue(matcher.Matches(sig, proxyWithSparse));
Assert.IsFalse(matcher.Matches(sig, emptyProxy));
```

- [ ] **Step 5: Commit**

```bash
git add ECS/Defines/IEntityMatcher.cs ECS/EntityMatcher.cs Test/EntityMatcherTestUnit.cs Test/SparseComponentTestUnit.cs
git commit -m "feat(core): evaluate EntityMatcher against archetype signature and SparseSetProxy"
```

---

### Task 7: `EntityQuery` streaming + read locks + World collection wrappers

**Files:**
- Create: `ECS/EntityQuery.cs`
- Modify: `ECS/World.cs`
- Modify: `ECS/EntityMatcherExtension.cs`
- Test: `Test/EntityQueryTestUnit.cs`
- Modify: `Test/WorldTestUnit.cs` (ensure collection Query still passes)

**Interfaces:**
- Consumes: `ArchetypeRegistry`, `IEntityMatcher.Matches`, read locks on `Archetype`
- Produces:
  - `public sealed class EntityQuery : IEnumerable<Entity>, IDisposable` (or disposable enumerator only — enumerator **must** be `IDisposable`)
  - `World.Query(IEntityMatcher matcher)` → `EntityQuery` / `IEnumerable<Entity>`
  - Existing `Query(matcher, ICollection<…>)` implemented as foreach into collection
  - On `GetEnumerator`: determine candidate archetypes from matcher dense conditions; `AddReadLock` each; iterate chunks/rows; sparse secondary filter; yield `Entity`
  - On enumerator `Dispose`: `RemoveReadLock` all
  - Early `break` must dispose (foreach does)

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void EntityQuery_YieldsMatchingEntities()
{
    var world = new World();
    var a = world.CreateEntity();
    a.CreateComponent<DenseProbe>();
    var b = world.CreateEntity();
    var matcher = EntityMatcher.With().OfAll<DenseProbe>();
    var ids = new List<ulong>();
    foreach (var e in world.Query(matcher))
        ids.Add(e.ID); // use actual Entity id property name from Entity.cs
    Assert.AreEqual(1, ids.Count);
}

[Test]
public void EntityQuery_DenseCreateWhileEnumerating_Throws()
{
    var world = new World();
    var e = world.CreateEntity();
    e.CreateComponent<DenseProbe>();
    var matcher = EntityMatcher.With().OfAll<DenseProbe>();
    Assert.Throws<InvalidOperationException>(() =>
    {
        foreach (var hit in world.Query(matcher))
            hit.CreateComponent<DenseProbe>();
    });
}
```

Confirm `Entity` id property name (`Id` vs `ID`) from `ECS/Entity.cs` before writing the test.

- [ ] **Step 2: Run tests to verify fail**

Run: `dotnet test --filter FullyQualifiedName~EntityQuery_ --verbosity normal`  
Expected: FAIL

- [ ] **Step 3: Implement EntityQuery + World wrappers**

```csharp
public sealed class EntityQuery : IEnumerable<Entity>
{
    public IEnumerator<Entity> GetEnumerator() => new Enumerator(...);
    // Enumerator : IEnumerator<Entity>, IDisposable
}

// World
public EntityQuery Query(IEntityMatcher matcher) => new EntityQuery(this, matcher);

public int Query(IEntityMatcher matcher, ICollection<ulong> result)
{
    var n = 0;
    foreach (var e in Query(matcher))
    {
        result.Add(e.Id);
        n++;
    }
    return n;
}
```

Candidate selection: archetypes whose signature satisfies dense all/any/none; if matcher has only sparse conditions, include **all** archetypes (including empty) then Proxy-filter.

- [ ] **Step 4: Run query tests**

Run: `dotnet test --filter FullyQualifiedName~EntityQuery_|FullyQualifiedName~World_Query --verbosity normal`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ECS/EntityQuery.cs ECS/World.cs ECS/EntityMatcherExtension.cs Test/EntityQueryTestUnit.cs Test/WorldTestUnit.cs
git commit -m "feat(core): add streaming EntityQuery with archetype read locks"
```

---

### Task 8: CommandBuffer rent + Playback modes

**Files:**
- Create: `ECS/Defines/CommandBufferFlag.cs`
- Create: `ECS/Defines/ICommandBuffer.cs`
- Create: `ECS/Managers/CommandBuffer.cs`
- Modify: `ECS/World.cs` (`RentCommandBuffer`)
- Test: `Test/CommandBufferTestUnit.cs`
- Modify: `Test/EntityQueryTestUnit.cs` (defer under lock then playback)

**Interfaces:**
- Produces:

```csharp
public enum CommandBufferFlag
{
    Default = AutoPlaybackOnDispose,
    AutoPlaybackOnDispose = 0,
    DiscardPendingOnDispose = 1,
    MustManualPlaybackOnDispose = 2,
}

public interface ICommandBuffer : IDisposable
{
    void CreateComponentDefer<T>(Entity entity) where T : struct, IComponent<T>;
    void CreateComponentDefer<T>(Entity entity, T initial) where T : struct, IComponent<T>;
    void DestroyComponentDefer<T>(Entity entity) where T : struct, IComponent<T>;
    void DestroyComponentDefer(Entity entity, ComponentRef component);
    void Playback();
}

// World
public ICommandBuffer RentCommandBuffer(CommandBufferFlag flag = CommandBufferFlag.Default);
```

Dispose behavior by flag as in spec.

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void CommandBuffer_Default_AutoPlaybackOnDispose()
{
    var world = new World();
    var e = world.CreateEntity();
    using (var buf = world.RentCommandBuffer())
        buf.CreateComponentDefer<DenseProbe>(e);
    Assert.IsTrue(e.HasComponent<DenseProbe>());
}

[Test]
public void CommandBuffer_MustManual_ThrowsIfDisposeWithoutPlayback()
{
    var world = new World();
    var e = world.CreateEntity();
    var buf = world.RentCommandBuffer(CommandBufferFlag.MustManualPlaybackOnDispose);
    buf.CreateComponentDefer<DenseProbe>(e);
    Assert.Throws<InvalidOperationException>(() => buf.Dispose());
}

[Test]
public void CommandBuffer_Discard_DropsPending()
{
    var world = new World();
    var e = world.CreateEntity();
    using (var buf = world.RentCommandBuffer(CommandBufferFlag.DiscardPendingOnDispose))
        buf.CreateComponentDefer<DenseProbe>(e);
    Assert.IsFalse(e.HasComponent<DenseProbe>());
}

[Test]
public void CommandBuffer_DeferDenseCreate_DuringQuery_ThenPlayback()
{
    var world = new World();
    var e = world.CreateEntity();
    e.CreateComponent<DenseProbe>();
    var matcher = EntityMatcher.With().OfAll<DenseProbe>();
    using (var buf = world.RentCommandBuffer(CommandBufferFlag.MustManualPlaybackOnDispose))
    {
        foreach (var hit in world.Query(matcher))
            buf.CreateComponentDefer<DenseProbe>(hit);
        // enumeration ended → locks released
        buf.Playback();
    }
    Assert.AreEqual(2, e.GetComponentCount<DenseProbe>());
}
```

- [ ] **Step 2: Run tests to verify fail**

Run: `dotnet test --filter FullyQualifiedName~CommandBuffer_ --verbosity normal`  
Expected: FAIL

- [ ] **Step 3: Implement buffer + pool rental**

Pool via `Pool<CommandBuffer>` similar to `EntityGraph.Pool`. Record commands as structs/tagged enum in a `List<>`. `Playback` applies in order through ComponentManager/Entity APIs (immediate path). `OnCreate`/`OnDestroy` only during Playback/immediate.

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter FullyQualifiedName~CommandBuffer_ --verbosity normal`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ECS/Defines/CommandBufferFlag.cs ECS/Defines/ICommandBuffer.cs ECS/Managers/CommandBuffer.cs ECS/World.cs Test/CommandBufferTestUnit.cs Test/EntityQueryTestUnit.cs
git commit -m "feat(core): add rented CommandBuffer with dispose playback modes"
```

---

### Task 9: Collector hybrid strategy C

**Files:**
- Modify: `ECS/Managers/EntityMatchManager.cs`
- Modify: `Test/EntityCollectorTestUnit.cs` (should stay green)
- Test: add cases in `Test/EntityCollectorTestUnit.cs` or `Test/SparseComponentTestUnit.cs` for dense migrate membership + sparse notification

**Interfaces:**
- Consumes: matcher dense/sparse split, archetype id on migrate events, existing component signals
- Produces: collector membership updates without read locks; dense path uses matcher→archetype-id set; sparse/RW use notifications + `Matches`/`IsRelevantComponent`

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void Collector_DenseSecondInstance_StillMatchedAfterMigrate()
{
    var world = new World();
    var e = world.CreateEntity();
    e.CreateComponent<DenseProbe>();
    var collector = EntityMatcher.With().OfAll<DenseProbe>().Build(world);
    collector.Flush();
    Assert.Contains(e.Id, collector.Collected.ToList()); // adapt to actual buffer API
    e.CreateComponent<DenseProbe>(); // migrate Count 1→2; still has DenseProbe
    collector.Flush();
    Assert.Contains(e.Id, collector.Collected.ToList());
}

[Test]
public void Collector_SparseAdd_PublishesAfterFlush()
{
    var world = new World();
    var e = world.CreateEntity();
    var collector = EntityMatcher.With().OfAll<SparseProbe>().Build(world);
    e.CreateComponent<SparseProbe>();
    Assert.IsEmpty(collector.Matching); // before flush — adapt to API
    collector.Flush();
    Assert.Contains(e.Id, collector.Matching.ToList());
}
```

Read `IEntityCollector` buffer property names before finalizing asserts (`Collected`/`Matching` are `IReadOnlyCollection<ulong>` etc.).

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test --filter FullyQualifiedName~Collector_DenseSecond|FullyQualifiedName~Collector_SparseAdd --verbosity normal`  
Expected: FAIL if still using RwComponents filter

- [ ] **Step 3: Rewire EntityMatchManager**

Replace `matcher.ComponentFilter(entityGraph.RwComponents)` with location→signature+proxy `Matches`. On dense migrate notifications, recompute membership via archetype index + sparse predicate. Keep Flush/flag semantics identical.

- [ ] **Step 4: Run collector suite**

Run: `dotnet test --filter FullyQualifiedName~EntityCollector|FullyQualifiedName~Collector_ --verbosity normal`  
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ECS/Managers/EntityMatchManager.cs Test/EntityCollectorTestUnit.cs Test/SparseComponentTestUnit.cs
git commit -m "feat(core): hybrid EntityCollector membership via archetype index and sparse notifications"
```

---

### Task 10: Regression sweep + known layout failures

**Files:**
- Possibly: `Test/IntegrationTestUnit.cs`, `Test/StressTestUnit.cs` (fix only if broken by API moves, not to preserve Store layout)
- Do **not** rewrite `ComponentManagerTestUnit` dense hole/`Allocated` asserts unless trivial; leave failing with a short comment at top of file or README note in commit body

- [ ] **Step 1: Run full test suite**

Run: `dotnet test --verbosity normal`  
Record failures.

- [ ] **Step 2: Fix semantic regressions only**

Fix Entity/Matcher/Collector/World/Query/CommandBuffer/Integration failures. For `ComponentManagerTestUnit` asserts that require dense soft-delete holes on `IComponent`, either:

- Annotate with `[Ignore("Dense components moved to ComponentChunk; owner will update")]` **only if** user previously allowed test edits for this class — spec allows failures without forcing edits; **prefer leaving red** over drive-by ignores unless CI must be green.

If CI must be green for merge: ask owner — for this plan, quarantine with `[Ignore(...)]` on layout-specific tests as last resort and commit as `fix(test): ignore dense ComponentStore layout asserts after chunk migration`.

- [ ] **Step 3: Re-run suite**

Run: `dotnet test --verbosity normal`  
Expected: All non-ignored tests PASS

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "fix(test): stabilize suites after archetype chunk migration"
```

---

## Spec coverage checklist

| Spec requirement | Task |
|------------------|------|
| `ISparseComponent` + routing | 1, 5 |
| Chunk + multiplicity signature | 2, 3, 5 |
| SparseSetProxy handles | 2, 5 |
| Proxy-only / empty archetype | 2, 5 |
| EntityGraph without RwComponents | 4 |
| Immediate dense migrate + lock throw | 5, 7 |
| CommandBuffer modes enum | 8 |
| EntityQuery `IEnumerable` + collection wrappers | 7 |
| Matcher Matches + unified OfAll | 6 |
| Collector hybrid C, no locks | 9 |
| No Store rename; layout tests may fail | 5, 10 |
| XML docs / English / exceptions | all tasks |

## Placeholder / consistency self-review

- Enum name `CommandBufferFlag` matches approved spec (exclusive modes, not `[Flags]`).
- `MustManualPlaybackOnDispose` spelling (Manual) matches spec.
- Entity id property must be verified against `ECS/Entity.cs` when implementing tests (`Id`).
- Default chunk capacity **64** fixed in Task 3.
- No TBD left for required behaviors; open knobs limited to exact type names already aliased in Interfaces blocks.

---

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-23-archetype-chunk-sparse-storage.md`.

**Two execution options:**

1. **Subagent-Driven (recommended)** — fresh subagent per task, review between tasks  
2. **Inline Execution** — execute tasks in this session with executing-plans checkpoints  

Which approach?
