# Design: Archetype Chunk + SparseSetProxy Storage

**Date:** 2026-07-23  
**Status:** Draft for review  
**Scope:** Internal storage migration toward Archetype Chunks for dense `IComponent`, SparseSet (`ComponentStore`) for `ISparseComponent`, with unified matcher semantics. Public Entity create/get/destroy/`ComponentRef` surface stays stable where possible; additive APIs only (`ISparseComponent`, command buffer, `EntityQuery`).

---

## 1. Goals and non-goals

### Goals

- Keep existing call-site patterns for `Entity.CreateComponent` / `Get*` / `Destroy*` / `ComponentRef` RO·RW.
- Store dense `IComponent` in **Archetype Chunks** (`ComponentChunk`).
- Store `ISparseComponent` in existing **`ComponentStore`** (rename to `ComponentSparse` is **out of scope**; owner will rename later).
- Allow **multiple instances** of the same component type per entity on both backends.
- Archetype signature for dense components includes **multiplicities** `(Type × Count)`.
- Unified matcher semantics: e.g. `OfAll<TSparse, TDense>` matches entities that have both.
- Expose efficient **`EntityQuery`** as a streaming **`IEnumerable<Entity>`** over matching archetypes with **read locks** bound to enumeration; keep fill-into-`ICollection` overloads as convenience wrappers.
- Keep **`EntityCollector`** notification-driven for sparse/revision changes, with archetype indexing for dense structural membership (hybrid). Split Query from Collector.

### Non-goals

- Renaming `ComponentStore` → `ComponentSparse`.
- Forcing edits to existing test sources in this design’s implementation phase (storage-layout-coupled `ComponentManager` tests may fail until the owner updates them).
- Pure archetype-only collectors (no notifications).
- Removing soft-delete/`Cleanup` behavior from the sparse `ComponentStore` path.

---

## 2. Decisions summary

| Topic | Decision |
|-------|----------|
| Chunk model | Archetype Chunk (not per-type paging alone) |
| Multi-instance | Allowed for Chunk and SparseSet |
| Dense archetype key | Multiset of `(Type, Count)` |
| Sparse vs archetype | Sparse **data** in `ComponentStore`; sparse **does not** change archetype on add/remove |
| Chunk secondary index | Every chunk row has `SparseSetProxy` (list of sparse `IComponentRefCore` handles) |
| Sparse-only entities | Live in **proxy-only** archetype (no dense columns) |
| `ISparseComponent` | `ISparseComponent<T> : IComponent<T>` |
| Default routing | Non-sparse `IComponent` → Chunk; `ISparseComponent` → Store |
| Dense structural timing | Immediate migrate when not read-locked; otherwise must use deferred commands |
| Sparse structural timing | Immediate Store + Proxy update; no chunk migrate |
| `EntityGraph` | Drop `RwComponents`; store archetype location (`ArchetypeId` + `Row` or equivalent) |
| Archetype read lock | Held by `EntityQuery` iteration; blocks dense migration |
| Collector locks | **Does not** hold archetype read locks |
| Deferred API | Caller **rents** `IDisposable` command buffer; buffer owns `*Defer` APIs; must `Playback()` before `Dispose` or throw |
| Immediate mutate while locked | Throw (must use buffer) |
| Query API | **`EntityQuery`**: primary = `IEnumerable<Entity>` (yield each match); retain fill-into-`ICollection` overloads as wrappers; split from Collector |
| Query locks | Read-lock matched archetypes for the enumeration lifetime (`GetEnumerator`…`Dispose`) |
| Collector strategy | Hybrid **C**: dense membership via archetype index; sparse + RW/revision via notifications |
| Matcher | Keep fluent `OfAll`/`OfAny`/`OfNone`; replace list-based `ComponentFilter` as primary eval with archetype + proxy `Matches` |
| Rename Store | Not in this work |

---

## 3. Architecture

```text
World / ComponentManager
├── Dense IComponent (not ISparseComponent)
│     └── Archetype → ComponentChunk[] (SoA columns + EntityIds + SparseSetProxy per row)
└── ISparseComponent
      └── ComponentStore (existing dense soft-delete store)
            └── handles also recorded in row SparseSetProxy

EntityGraph
├── Mask, Generation, WishDestroy
└── ArchetypeId + Row  (no RwComponents)

EntityQuery : IEnumerable<Entity> (+ IDisposable enumerator / query object)
  └── read-locks matched archetypes while enumerating
EntityCollector (signals + dense archetype membership index; no read locks)
CommandBuffer (rented, IDisposable; Playback then Dispose)
```

### 3.1 ComponentChunk

- Fixed capacity per chunk (implementation default e.g. 64 or 128; configurable later if needed).
- Columns:
  - `EntityIds`
  - For each dense `(T, Count)`: `Count` logical columns of `T` (or one slab with stride)
  - `SparseSetProxy` per row: list of sparse component ref cores for that entity
- Dense `ComponentRef` binds to chunk locator + offset/slot; migration calls `Relocate` on surviving cores.

### 3.2 SparseSetProxy

- Stores **handles** (`IComponentRefCore` or equivalent), not component payloads.
- Payload remains in `ComponentStore`.
- Updated whenever sparse components are created/destroyed on that entity.
- Used for secondary filter when an `EntityMatcher` includes sparse types, and for `GetComponents` enumeration of sparse instances.

### 3.3 Archetype signature

- Built **only** from dense (non-sparse) components and their counts.
- Examples: `{Position×1, Velocity×1}` ≠ `{Position×2}`.
- Empty dense signature = proxy-only archetype (entities with zero dense components, including sparse-only and empty-composition entities that still occupy a row).

---

## 4. EntityGraph and entity APIs

### EntityGraph

- Remove `RwComponents` as the source of truth for composition queries.
- Add stable location: archetype + row (and any generation/version needed to detect stale location after migrate).
- Preserve `Mask`, `Generation`, `WishDestroy` semantics.

### Entity accessors

- `Has` / `Get` / `GetComponents` / `GetComponentCount`:
  - Dense: read columns for that row (first instance for single-get/destroy-by-type, all for get-all).
  - Sparse: consult `SparseSetProxy` + Store.
- Multi-instance rules match today’s Entity/Graph tests: first match for `GetComponent` / `DestroyComponent<T>()`; all instances for `GetComponents<T>`.

---

## 5. Locks, CommandBuffer, and structural changes

### Read locks

- `EntityQuery` acquires read locks on archetypes it scans for the **enumeration lifetime**:
  - Primary API is streaming `IEnumerable<Entity>`: as soon as a row matches (dense + optional Proxy filter), yield one `Entity`.
  - Locks are taken when enumeration starts and released when the enumerator/query is **disposed** (`foreach` disposes the enumerator).
  - Fill-into-`ICollection` overloads are convenience wrappers: enumerate under the same lock rules, then return (locks released when the helper’s enumeration ends).
- While read-locked, **dense** create/destroy/count-changing operations that would migrate entities **throw**.
- **Sparse** create/destroy remains allowed (Store + Proxy only).

### CommandBuffer (rented)

```csharp
using (var buf = world.RentCommandBuffer()) // IDisposable, pooled
{
    buf.CreateComponentDefer<T>(entity, /* optional value */);
    buf.DestroyComponentDefer(/* ... */);
    // ... finish EntityQuery / release read locks ...
    buf.Playback(); // apply in order
} // Dispose returns to pool; if Playback was not called → throw
```

- Defer means **enqueue a command**; it does not allocate/migrate until `Playback`.
- `OnCreate` / `OnDestroy` run at real allocate/release time (immediate path or `Playback`), not at enqueue.
- Component created/removed/changed signals for collectors fire at real mutation time (including during `Playback`).
- `Dispose` without prior `Playback` is illegal → **throw** (no silent drop), then buffer may still be returned/invalidated per implementation rules.

---

## 6. EntityMatcher

### Keep

- Fluent builders: `OfAll` / `OfAny` / `OfNone` / mask.
- Presence semantics: type required/forbidden means **count ≥ 1** / **count == 0** (multiplicity does not change matcher DSL).
- `IsRelevantComponent(Type)` for `RelatedComponentOnly` (empty matcher remains relevant to all types).

### Change

- Primary evaluation becomes composition against **dense archetype signature + SparseSetProxy** (name e.g. `Matches(...)`), not `ComponentFilter(RwComponents)`.
- `ComponentFilter(IReadOnlyCollection<IComponentRefCore>)` is retired as the primary contract (obsolete or thin adapter only if needed during transition).
- Internally split conditions into **dense** vs **sparse** sets so:
  - `EntityQuery` can select candidate archetypes from dense conditions first,
  - then secondary-filter rows with sparse conditions via Proxy,
  - collectors can maintain dense archetype membership sets.

---

## 7. EntityQuery (split from Collector)

`EntityQuery` is the scan/iteration API. It is **not** part of `EntityCollector` and must not share Flush/buffer semantics with collectors.

### Primary: `IEnumerable<Entity>`

```csharp
foreach (var entity in world.Query(matcher)) // or matcher.Query(world)
{
    // Each matching entity is produced when found (lazy over archetypes/chunks).
    // Read locks are held for the duration of this enumeration.
}
```

Requirements:

- Implement streaming enumeration: advance archetype/chunk cursors; on match, `yield` / return one `Entity`.
- Enumerator (and/or query object) must be **`IDisposable`** so locks are released reliably when enumeration ends or is abandoned.
- Matching rules are the same `IEntityMatcher` rules (dense archetype candidates + sparse Proxy secondary filter).

### Secondary: fill `ICollection` overloads

- Keep existing-style `Query(matcher, ICollection<ulong|Entity>)` (and matcher extensions) as **wrappers** over the enumerable path.
- They must not reintroduce a separate matching implementation.

### Interaction with structural changes

During an active (locked) `EntityQuery`, callers that need dense structural changes must rent a `CommandBuffer`, enqueue Defer commands, **end the enumeration** (release locks), then `Playback`.

---

## 8. EntityCollector (hybrid strategy C)

### Unchanged externally

- Flags, `Flush`, Matching / Clashing / Changed / Collected deferral until Flush.
- No archetype read locks.

### Internals

1. **Dense structural membership:** maintain matcher → set of matching archetype ids (from dense conditions). On dense migrate, update membership via archetype set difference; apply sparse predicate via Proxy when the matcher has sparse conditions.
2. **Sparse structural changes:** notification path; gate with `IsRelevantComponent`; re-`Matches` using Proxy.
3. **Revision / RW (`Changed`):** notification path only (archetypes do not observe field writes).
4. Events from `Playback` are normal structural notifications (dedup still until Flush).

---

## 9. World / managers touch list

| Area | Adjustment |
|------|------------|
| `ComponentManager` | Route by sparse interface; own archetypes/chunks/locks; rent command buffers; keep `ComponentStore` for sparse |
| `EntityManager` | Stop syncing `RwComponents`; update entity location on migrate; destroy removes chunk row + sparse comps |
| `World` | `EntityQuery` entry points; `RentCommandBuffer`; `EndTick` still cleans sparse store as today; optional note: does **not** auto-Playback rented buffers |
| `EntityMatchManager` | Eval via new `Matches`; hybrid membership index |
| `Entity` / extensions | Same façade; throw when dense immediate mutate under read lock |
| New types | `ComponentChunk`, archetype registry, `SparseSetProxy`, command buffer interfaces, `EntityQuery` |

---

## 10. Error handling

Explicit exceptions (no silent failure):

- Dense immediate structural change while archetype read-locked.
- Command buffer `Dispose` without `Playback`.
- Existing illegal ops: invalid entity, foreign component destroy, double destroy, missing destroy-by-type, etc.

---

## 11. Testing strategy

### Expected to remain green (semantics)

- Entity create/get/destroy/Has, multi-instance first/all semantics.
- Matcher OfAll/Any/None, mask, `IsRelevantComponent` (including empty matcher).
- Collector Flush / flags / RelatedComponentOnly / dedup (wired through new eval + hybrid index).
- World query result membership (via `EntityQuery` implementation).
- Component lifecycle hooks timing relative to real allocate/release.

### May fail until owner updates (layout-coupled)

- Tests that assert `ComponentStore` holes, `Allocated` including soft-deleted slots, `NeedRearrange`, `ComponentGroups` layout for types that become dense chunk-backed by default.

### New coverage required

- Dense multiplicity archetypes and migration.
- Proxy-only archetype; sparse add/remove without migrate.
- `OfAll` mixed sparse + dense.
- Command buffer rent / Playback / dispose-without-playback throws.
- Immediate dense mutate under `EntityQuery` lock throws.
- `EntityQuery` `IEnumerable` streaming + lock lifetime (including early `break` / dispose).
- Fill-`ICollection` overloads delegate to the same matching/lock behavior.
- Collector hybrid: dense migrate membership vs sparse notification vs RW Changed.

---

## 12. Implementation notes (guidance, not a plan)

- Prefer small types: archetype registry, chunk, proxy, command buffer, query scope — avoid stuffing everything into `ComponentManager` without seams.
- Preserve `ComponentRef` core identity equality and versioned `NotNull`.
- XML docs on new public APIs (purpose, params, returns, exceptions).
- English identifiers/comments; `m_` private fields; `I*` / `*Manager` / `*Extension` / `*TestUnit` naming per project rules.

---

## 13. Open implementation knobs (non-blocking)

These may be chosen during implementation without changing the design intent:

- Exact default chunk capacity.
- Exact public names for buffer interface (`ICommandBuffer` vs similar) as long as rent + Defer + Playback + IDisposable semantics hold.
- Exact public type name for the query enumerable (`EntityQuery` vs returning `IEnumerable<Entity>` directly) as long as streaming + disposable lock lifetime hold.
- Whether obsolete `ComponentFilter(list)` remains as a temporary adapter.

---

## 14. Approval

Please review this spec and call out changes before an implementation plan is written.
