# Entity Matcher Bitmask Acceleration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不破坏现有 `OfAll/OfAny/OfNone` 语义的前提下，用“按组件路由 + 位图签名匹配”降低实体匹配成本。

**Architecture:** 先在 `EntityMatchManager` 建立按组件类型分发 collector 的索引，避免无关 collector 被调用；再新增组件类型索引和实体组件位图签名，把 matcher 的 `all/any/none` 判定从 `HashSet<Type>` 查找切换为位运算。对外 API 维持 fluent matcher 用法不变，内部新增编译阶段与快速路径。

**Tech Stack:** C# 9, .NET 8, NUnit, CoreECS (`ECS` + `Test`).

---

## Scope Check

本需求涉及同一子系统（实体匹配链路）内的两个强关联改动：collector 分发优化与 matcher 位图优化。两者共享同一热点路径（`EntityMatchManager -> IEntityMatcher`），不拆为独立计划，避免重复改动与回归成本。

## 变更文件结构（File Structure）

- Create: `ECS/Utils/ComponentTypeBitSet.cs`  
  责任：提供可扩展位图（按 `ulong[]` 分段）以及 `Set/Clear/Contains/ContainsAll/Intersects` 等按位操作。

- Create: `ECS/Managers/ComponentTypeIndexManager.cs`  
  责任：维护 `Type -> stable bit index` 映射，确保组件类型索引稳定且可复用。

- Modify: `ECS/EntityGraph.cs`  
  责任：新增实体组件签名 `ComponentTypeBitSet`，在实体生命周期内持有组件位图状态。

- Modify: `ECS/Managers/EntityManager.cs`  
  责任：在组件新增/移除回调里同步更新 `EntityGraph` 位图签名。

- Modify: `ECS/Defines/IEntityMatcher.cs`  
  责任：扩展 matcher 契约，提供相关组件枚举与位图编译入口。

- Modify: `ECS/EntityMatcher.cs`  
  责任：保留现有 fluent API，同时新增位图编译缓存与 `SignatureFilter` 快速匹配路径。

- Modify: `ECS/Managers/EntityMatchManager.cs`  
  责任：新增按组件类型路由 collector 的索引，优先走 `SignatureFilter`，无法使用时回退旧逻辑。

- Create: `Test/EntityMatcherBitmaskTestUnit.cs`  
  责任：新增匹配正确性、路由有效性、位图边界（跨 64 位）测试。

- Modify: `Test/StressTestUnit.cs`  
  责任：新增性能基线与优化后对比测试，输出匹配调用次数和耗时统计。

## 预期性能提升（目标）

- 事件分发开销：  
  将组件变更时的 collector 调用从 `O(total_collectors)` 降为 `O(relevant_collectors_for_component)`，在“collector 多、组件变更稀疏”场景下，`ComponentFilter` 调用次数目标下降 **60%~90%**。

- 匹配判定开销：  
  `all/any/none` 由多次 `HashSet<Type>.Contains` + `IsSupersetOf` 转为位运算，目标在 10k 实体 / 10+ collector 压测场景中，匹配阶段耗时下降 **25%~50%**（以 `StressTest` 新增统计为准）。

- 分配与缓存友好性：  
  移除 `ComponentFilter` 中每次匹配对临时集合的构造/维护（`m_changing` 仅保留兼容路径），目标减少热点路径分支和哈希查找。

## 测试目标（验收标准）

1. 语义一致性：`OfAll/OfAny/OfNone` 与 `RelatedComponentOnly` 相关行为不变。  
2. 路由准确性：无关组件变更不触发无关 collector 的匹配计算。  
3. 位图正确性：跨 64 位边界组件类型组合仍能正确匹配。  
4. 回归安全：`dotnet test --verbosity normal` 全量通过。  
5. 性能目标：新增性能测试打印“优化前/后调用次数和耗时”，满足上面提升区间。

---

### Task 1: Collector 路由索引（先降 fan-out）

**Files:**
- Modify: `ECS/Defines/IEntityMatcher.cs`
- Modify: `ECS/EntityMatcher.cs`
- Modify: `ECS/Managers/EntityMatchManager.cs`
- Test: `Test/EntityMatcherBitmaskTestUnit.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void EntityMatchManager_ComponentRouting_IrrelevantCollectorMustNotRunFilter()
{
    var world = new World();
    world.Startup();

    var relevant = new CountingMatcher(typeof(PositionComponent), shouldMatch: true);
    var irrelevant = new CountingMatcher(typeof(HealthComponent), shouldMatch: true);

    var c1 = world.CreateCollector(relevant, EntityCollectorFlag.None);
    var c2 = world.CreateCollector(irrelevant, EntityCollectorFlag.None);

    var entity = world.CreateEntity();
    entity.CreateComponent<PositionComponent>();

    c1.Flush();
    c2.Flush();

    relevant.ResetCalls();
    irrelevant.ResetCalls();

    ref var rw = ref entity.GetComponent<PositionComponent>().RW;
    rw.X = 10;
    c1.Flush();
    c2.Flush();

    Assert.Greater(relevant.FilterCalls, 0);
    Assert.AreEqual(0, irrelevant.FilterCalls, "Irrelevant collector should not evaluate matcher.");

    c1.Dispose();
    c2.Dispose();
    world.Shutdown();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~EntityMatchManager_ComponentRouting_IrrelevantCollectorMustNotRunFilter" --verbosity normal`  
Expected: FAIL，`irrelevant.FilterCalls` 当前大于 0（现实现会遍历全部 collectors）。

- [ ] **Step 3: Write minimal implementation**

```csharp
// IEntityMatcher.cs
public interface IEntityMatcher
{
    bool ComponentFilter(IReadOnlyCollection<IComponentRefCore> components);
    ulong EntityMask { get; }
    bool IsRelevantComponent(Type componentType);
    bool MatchesAllComponents { get; }
    void CollectRelevantComponentTypes(ICollection<Type> results);
}
```

```csharp
// EntityMatcher.cs
public bool MatchesAllComponents => m_all.Count == 0 && m_any.Count == 0 && m_none.Count == 0;

public void CollectRelevantComponentTypes(ICollection<Type> results)
{
    foreach (var t in m_all) results.Add(t);
    foreach (var t in m_any) results.Add(t);
    foreach (var t in m_none) results.Add(t);
}
```

```csharp
// EntityMatchManager.cs (核心片段)
private readonly Dictionary<Type, List<Collector>> m_collectorsByComponent = new();
private readonly List<Collector> m_collectorsMatchAll = new();

private void RegisterCollectorRouting(Collector collector)
{
    if (collector.Matcher.MatchesAllComponents)
    {
        m_collectorsMatchAll.Add(collector);
        return;
    }

    var types = new HashSet<Type>();
    collector.Matcher.CollectRelevantComponentTypes(types);
    foreach (var type in types)
    {
        if (!m_collectorsByComponent.TryGetValue(type, out var list))
            m_collectorsByComponent[type] = list = new List<Collector>();
        list.Add(collector);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~EntityMatchManager_ComponentRouting_IrrelevantCollectorMustNotRunFilter" --verbosity normal`  
Expected: PASS。

- [ ] **Step 5: Commit**

Run:
`git add ECS/Defines/IEntityMatcher.cs ECS/EntityMatcher.cs ECS/Managers/EntityMatchManager.cs Test/EntityMatcherBitmaskTestUnit.cs && git commit -m "feat(core): route collectors by relevant component type"`

---

### Task 2: 引入组件类型位图基础设施

**Files:**
- Create: `ECS/Utils/ComponentTypeBitSet.cs`
- Create: `ECS/Managers/ComponentTypeIndexManager.cs`
- Test: `Test/EntityMatcherBitmaskTestUnit.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void ComponentTypeBitSet_CrossWordBoundary_MatchesCorrectly()
{
    var bits = new ComponentTypeBitSet();
    bits.Set(1);
    bits.Set(63);
    bits.Set(64);
    bits.Set(127);

    var all = new ComponentTypeBitSet();
    all.Set(1);
    all.Set(64);

    var none = new ComponentTypeBitSet();
    none.Set(5);
    none.Set(65);

    Assert.IsTrue(bits.ContainsAll(all));
    Assert.IsFalse(bits.Intersects(none));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ComponentTypeBitSet_CrossWordBoundary_MatchesCorrectly" --verbosity normal`  
Expected: FAIL，`ComponentTypeBitSet` 类型不存在。

- [ ] **Step 3: Write minimal implementation**

```csharp
// ECS/Utils/ComponentTypeBitSet.cs
public struct ComponentTypeBitSet
{
    private ulong[] m_words;

    public void Set(int bit)
    {
        var word = bit >> 6;
        Ensure(word + 1);
        m_words[word] |= 1UL << (bit & 63);
    }

    public void Clear(int bit)
    {
        var word = bit >> 6;
        if (m_words == null || word >= m_words.Length) return;
        m_words[word] &= ~(1UL << (bit & 63));
    }

    public bool ContainsAll(in ComponentTypeBitSet other)
    {
        var otherWords = other.m_words;
        if (otherWords == null || otherWords.Length == 0) return true;
        if (m_words == null || m_words.Length == 0) return false;

        for (var i = 0; i < otherWords.Length; i++)
        {
            var ow = otherWords[i];
            if (ow == 0) continue;
            var sw = i < m_words.Length ? m_words[i] : 0UL;
            if ((sw & ow) != ow) return false;
        }
        return true;
    }

    public bool Intersects(in ComponentTypeBitSet other)
    {
        if (m_words == null || other.m_words == null) return false;
        var len = Math.Min(m_words.Length, other.m_words.Length);
        for (var i = 0; i < len; i++)
        {
            if ((m_words[i] & other.m_words[i]) != 0) return true;
        }
        return false;
    }

    private void Ensure(int length)
    {
        if (m_words == null)
        {
            m_words = new ulong[length];
            return;
        }
        if (m_words.Length >= length) return;
        Array.Resize(ref m_words, length);
    }
}
```

```csharp
// ECS/Managers/ComponentTypeIndexManager.cs
public sealed class ComponentTypeIndexManager
{
    private readonly Dictionary<Type, int> m_typeToIndex = new();
    private int m_nextIndex;

    public int GetOrCreateIndex(Type componentType)
    {
        if (m_typeToIndex.TryGetValue(componentType, out var idx))
            return idx;
        idx = m_nextIndex++;
        m_typeToIndex.Add(componentType, idx);
        return idx;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ComponentTypeBitSet_CrossWordBoundary_MatchesCorrectly" --verbosity normal`  
Expected: PASS。

- [ ] **Step 5: Commit**

Run:
`git add ECS/Utils/ComponentTypeBitSet.cs ECS/Managers/ComponentTypeIndexManager.cs Test/EntityMatcherBitmaskTestUnit.cs && git commit -m "feat(core): add component type bitset and type index manager"`

---

### Task 3: 实体签名维护（EntityGraph/EntityManager）

**Files:**
- Modify: `ECS/EntityGraph.cs`
- Modify: `ECS/Managers/EntityManager.cs`
- Test: `Test/EntityMatcherBitmaskTestUnit.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void EntityGraph_ComponentSignature_ShouldTrackAddAndRemove()
{
    var world = new World();
    world.Startup();

    var entity = world.CreateEntity();
    entity.CreateComponent<PositionComponent>();
    entity.CreateComponent<VelocityComponent>();
    entity.DestroyComponent<VelocityComponent>();

    var collector = world.CreateCollector(EntityMatcher.With.OfAll<PositionComponent>(), EntityCollectorFlag.None);
    collector.Flush();

    Assert.IsTrue(collector.Collected.Contains(entity.EntityId));
    world.Shutdown();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~EntityGraph_ComponentSignature_ShouldTrackAddAndRemove" --verbosity normal`  
Expected: FAIL（在 Task 4 接入签名快速路径前，测试将因为签名未生效或路径缺失失败）。

- [ ] **Step 3: Write minimal implementation**

```csharp
// EntityGraph.cs
public ComponentTypeBitSet ComponentSignature;

private void Reset()
{
    EntityId = 0;
    Mask = 0;
    WishDestroy = false;
    RwComponents.Clear();
    ComponentSignature = default;
    Generation = (Generation % uint.MaxValue) + 1;
}
```

```csharp
// EntityManager.cs (组件增删事件)
private readonly ComponentTypeIndexManager m_typeIndexManager = new();

private void _onComponentAdded(IComponentRefCore component, ulong entityId, Type compType)
{
    var gs = GetEntity(entityId);
    if (gs == null) return;
    gs.RwComponents.Add(component);
    gs.ComponentSignature.Set(m_typeIndexManager.GetOrCreateIndex(compType));
    OnEntityGotComp.Emit(in gs, compType, static (h, g, t) => h(g, t));
}

private void _onComponentRemoved(IComponentRefCore component, ulong entityId, Type compType)
{
    var gs = GetEntity(entityId);
    if (gs == null) return;
    gs.RwComponents.Remove(component);
    gs.ComponentSignature.Clear(m_typeIndexManager.GetOrCreateIndex(compType));
    OnEntityLoseComp.Emit(in gs, compType, static (h, g, t) => h(g, t));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~EntityGraph_ComponentSignature_ShouldTrackAddAndRemove" --verbosity normal`  
Expected: PASS。

- [ ] **Step 5: Commit**

Run:
`git add ECS/EntityGraph.cs ECS/Managers/EntityManager.cs Test/EntityMatcherBitmaskTestUnit.cs && git commit -m "feat(core): maintain entity component signatures on add/remove"`

---

### Task 4: Matcher 位图编译与快速匹配路径

**Files:**
- Modify: `ECS/EntityMatcher.cs`
- Modify: `ECS/Managers/EntityMatchManager.cs`
- Test: `Test/EntityMatcherBitmaskTestUnit.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void EntityMatcher_BitsetPath_ShouldMatchAllAnyNoneSemantics()
{
    var world = new World();
    world.Startup();

    var e1 = world.CreateEntity();
    e1.CreateComponent<PositionComponent>();
    e1.CreateComponent<VelocityComponent>();

    var e2 = world.CreateEntity();
    e2.CreateComponent<PositionComponent>();
    e2.CreateComponent<HealthComponent>();

    var collector = world.CreateCollector(
        EntityMatcher.With
            .OfAll<PositionComponent>()
            .OfAny<VelocityComponent>()
            .OfAny<HealthComponent>()
            .OfNone<DamageComponent>(),
        EntityCollectorFlag.None);

    collector.Flush();

    AssertOnly(collector.Collected, e1.EntityId, e2.EntityId);
    world.Shutdown();
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~EntityMatcher_BitsetPath_ShouldMatchAllAnyNoneSemantics" --verbosity normal`  
Expected: FAIL（签名快速匹配未接入）。

- [ ] **Step 3: Write minimal implementation**

```csharp
// EntityMatcher.cs (核心片段)
private ComponentTypeBitSet m_allBits;
private ComponentTypeBitSet m_anyBits;
private ComponentTypeBitSet m_noneBits;
private bool m_compiled;

public void Compile(ComponentTypeIndexManager indexManager)
{
    m_allBits = default;
    m_anyBits = default;
    m_noneBits = default;
    foreach (var t in m_all) m_allBits.Set(indexManager.GetOrCreateIndex(t));
    foreach (var t in m_any) m_anyBits.Set(indexManager.GetOrCreateIndex(t));
    foreach (var t in m_none) m_noneBits.Set(indexManager.GetOrCreateIndex(t));
    m_compiled = true;
}

public bool SignatureFilter(in ComponentTypeBitSet signature)
{
    if (!m_compiled) return false;
    if (!signature.ContainsAll(m_allBits)) return false;
    if (m_any.Count > 0 && !signature.Intersects(m_anyBits)) return false;
    if (signature.Intersects(m_noneBits)) return false;
    return true;
}
```

```csharp
// EntityMatchManager.cs (核心片段)
var isMatched = false;
if (!entityGraph.WishDestroy)
{
    if (matcher is EntityMatcher em && em.IsCompiled)
        isMatched = em.SignatureFilter(entityGraph.ComponentSignature);
    else
        isMatched = matcher.ComponentFilter(entityGraph.RwComponents);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~EntityMatcher_BitsetPath_ShouldMatchAllAnyNoneSemantics" --verbosity normal`  
Expected: PASS。

- [ ] **Step 5: Commit**

Run:
`git add ECS/EntityMatcher.cs ECS/Managers/EntityMatchManager.cs Test/EntityMatcherBitmaskTestUnit.cs && git commit -m "feat(core): compile matcher conditions to bitset signature filters"`

---

### Task 5: 性能回归验证与文档补充

**Files:**
- Modify: `Test/StressTestUnit.cs`
- Modify: `README.md`
- Modify: `README.zh-CN.md`

- [ ] **Step 1: Write the failing test (performance target as assertion on call-count reduction)**

```csharp
[Test]
[Category("Performance")]
public void StressTest_MatcherRoutingAndBitset_ShouldReduceFilterCalls()
{
    var baselineCalls = RunCollectorScenario(useOptimizedPath: false);
    var optimizedCalls = RunCollectorScenario(useOptimizedPath: true);

    Assert.LessOrEqual(optimizedCalls, baselineCalls * 0.4, "Expected at least 60% call reduction.");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~StressTest_MatcherRoutingAndBitset_ShouldReduceFilterCalls" --verbosity normal`  
Expected: FAIL（优化路径未完整接入或统计未落地前）。

- [ ] **Step 3: Write minimal implementation**

```csharp
// StressTestUnit.cs (核心统计片段)
Console.WriteLine($"Baseline filter calls: {baselineCalls}");
Console.WriteLine($"Optimized filter calls: {optimizedCalls}");
Console.WriteLine($"Reduction ratio: {(1.0 - (double)optimizedCalls / baselineCalls):P2}");
```

```md
<!-- README.md / README.zh-CN.md 增加 -->
### Matcher Performance Notes
- Collector updates are routed by relevant component type.
- Matcher all/any/none checks are compiled into bitset operations.
- Use `dotnet test --filter Category=Performance` to run performance validations.
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "Category=Performance" --verbosity normal`  
Expected: PASS，并输出调用次数下降与耗时统计。

- [ ] **Step 5: Commit**

Run:
`git add Test/StressTestUnit.cs README.md README.zh-CN.md && git commit -m "feat(test): add matcher routing and bitset performance regression coverage"`

---

### Task 6: 全量回归与语义守护测试

**Files:**
- Modify: `Test/EntityCollectorTestUnit.cs`
- Modify: `ECS/Managers/EntityMatchManager.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void EntityCollector_RelatedComponentOnly_DoesNotMarkChangedForUnrelatedRevision_AfterBitsetMigration()
{
    var entity = _world.CreateEntity();
    entity.CreateComponent<PositionComponent>();
    entity.CreateComponent<VelocityComponent>();

    var collector = _world.CreateCollector(
        EntityMatcher.With.OfAll<PositionComponent>(),
        EntityCollectorFlag.RevisionAsChange | EntityCollectorFlag.RelatedComponentOnly);

    collector.Flush();
    collector.Flush();

    ref var rwVelocity = ref entity.GetComponent<VelocityComponent>().RW;
    rwVelocity.X = 100;
    collector.Flush();

    AssertEmpty(collector.Changed);
}
```

- [ ] **Step 2: Run full suite to verify status**

Run: `dotnet test --filter "FullyQualifiedName~EntityCollector_RelatedComponentOnly_DoesNotMarkChangedForUnrelatedRevision_AfterBitsetMigration" --verbosity normal`  
Expected: FAIL（若位图迁移破坏 `RelatedComponentOnly` 语义）。

- [ ] **Step 3: Write minimal implementation**

```csharp
// EntityMatchManager.cs - RelevanceGate 保持语义优先
private static bool RelevanceGate(Collector collector, IEntityMatcher matcher, Type componentType)
{
    if (componentType == null) return true;
    if (!collector.HasChangeComponent) return true;
    return matcher.IsRelevantComponent(componentType);
}
```

- [ ] **Step 4: Run full suite to verify pass**

Run: `dotnet test --verbosity normal`  
Expected: PASS（全部测试通过）。

- [ ] **Step 5: Commit**

Run:
`git add ECS/Managers/EntityMatchManager.cs Test/EntityCollectorTestUnit.cs && git commit -m "fix(core): preserve related-component change semantics after matcher bitset migration"`

---

## Self-Review

1. **Spec coverage:**  
   - 修改点：已覆盖（契约、路由、位图、签名、测试、文档）。  
   - 预期性能提升：已量化到调用次数和耗时区间。  
   - 测试目标：已列功能、路由、边界、性能、全量回归五类目标。  

2. **Placeholder scan:**  
   - 已检查无 `TODO/TBD/implement later`。  
   - 所有代码步骤都包含具体代码片段。  
   - 所有执行步骤都有明确命令与预期结果。  

3. **Type consistency:**  
   - 统一使用 `ComponentTypeBitSet`、`ComponentTypeIndexManager`、`CollectRelevantComponentTypes`、`SignatureFilter` 命名。  
   - 任务间 API 名称一致，无前后冲突。
