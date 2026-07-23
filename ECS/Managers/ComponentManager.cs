using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CoreECS.Defines;
using CoreECS.Utils;

namespace CoreECS.Managers
{
    /// <summary>
    /// Delegate for component creation events.
    /// </summary>
    /// <param name="component">The component reference core</param>
    /// <param name="entityId">The ID of the entity that owns the component</param>
    /// <param name="compType">The type of the component that was created</param>
    public delegate void ComponentCreated(IComponentRefCore component, ulong entityId, Type compType);

    /// <summary>
    /// Delegate for component destruction events.
    /// </summary>
    /// <param name="component">The component reference core</param>
    /// <param name="entityId">The ID of the entity that owned the component</param>
    /// <param name="compType">The type of the component that was destroyed</param>
    public delegate void ComponentDestroyed(IComponentRefCore component, ulong entityId, Type compType);

    /// <summary>
    /// Delegate for component revision change events.
    /// </summary>
    /// <param name="component">The component reference core</param>
    /// <param name="entityId">The ID of the entity that owns the component</param>
    /// <param name="compType">The type of the component that changed</param>
    public delegate void ComponentChanged(IComponentRefCore component, ulong entityId, Type compType);
    
    /// <summary>
    /// Core implementation of IComponentRefCore that holds the locator, offset, and version
    /// information for a component reference.
    /// </summary>
    public class ComponentRefCore : IComponentRefCore
    {
        /// <summary>
        /// Object pool for ComponentRefCore instances to reduce memory allocations.
        /// </summary>
        public static readonly Pool<ComponentRefCore> Pool = new(
            createFunc: () => new ComponentRefCore(),
            returnAction: x => x.Invalidate());

        /// <summary>
        /// Gets the locator for this component reference.
        /// </summary>
        public IComponentRefLocator RefLocator => m_refLocator;

        /// <summary>
        /// Gets the memory offset of this component reference.
        /// </summary>
        public int Offset => m_offset;

        /// <summary>
        /// Gets the version of this component reference.
        /// </summary>
        public uint Version => m_version;

        /// <summary>
        /// Backing field for the locator.
        /// </summary>
        private IComponentRefLocator m_refLocator;

        /// <summary>
        /// Backing field for the offset.
        /// </summary>
        private int m_offset;

        /// <summary>
        /// Backing field for the version.
        /// </summary>
        private uint m_version;

        /// <summary>
        /// Initializes a new instance of the ComponentRefCore class.
        /// </summary>
        /// <param name="refLocator">The locator for this component reference</param>
        /// <param name="offset">The memory offset of this component reference</param>
        /// <param name="version">The version of this component reference</param>
        public ComponentRefCore(IComponentRefLocator refLocator, int offset, uint version)
        {
            m_refLocator = refLocator;
            m_offset = offset;
            m_version = version;
        }

        /// <summary>
        /// Private constructor for object pool usage.
        /// </summary>
        private ComponentRefCore()
        {
            m_refLocator = null;
            m_offset = -1;
            m_version = 0;
        }

        /// <summary>
        /// Changes the location of this component reference.
        /// This is used internally when components are moved in memory.
        /// </summary>
        /// <param name="offset">The new offset</param>
        public void Relocate(int offset)
        {
            m_offset = offset;
        }
        
        /// <summary>
        /// Allocates this component reference with the specified locator, offset, and version.
        /// </summary>
        /// <param name="locator">The new locator</param>
        /// <param name="offset">The new offset</param>
        /// <param name="version">The new version</param>
        public void Allocate(IComponentRefLocator locator, int offset, uint version)
        {
            m_refLocator = locator;
            m_offset = offset;
            m_version = version;
        }
        
        /// <summary>
        /// Invalidates this component reference.
        /// </summary>
        public void Invalidate()
        {
            m_refLocator = null;
            m_offset = -1;
            m_version = 0;
        }
    }

    /// <summary>
    /// Abstract base class for component stores that manage components in the world.
    /// Provides the basic interface for allocating and deallocating components.
    /// </summary>
    public abstract class ComponentStore
    {
        /// <summary>
        /// Gets the reference locator of this store.
        /// </summary>
        public abstract IComponentRefLocator RefLocator { get; }

        /// <summary>
        /// Allocates a component in this store for the specified entity.
        /// </summary>
        /// <param name="entityId">The ID of the entity that will own the component</param>
        /// <returns>The position of the allocated component in the store</returns>
        public abstract int Fix(ulong entityId);

        /// <summary>
        /// Releases a component in this store at the specified position.
        /// </summary>
        /// <param name="pos">The position of the component to release</param>
        /// <returns>True if the component was successfully released, false otherwise</returns>
        public abstract bool Release(int pos);

        /// <summary>
        /// Rearranges the components in this store to optimize memory usage.
        /// </summary>
        /// <returns>The number of invalid component slots removed from the allocated range.</returns>
        public abstract int Rearrange();

        /// <summary>
        /// Gets a value indicating whether this store contains invalid slots that can be compacted.
        /// </summary>
        public bool NeedRearrange { get; protected set; }

        /// <summary>
        /// Sets the callbacks used by this store.
        /// </summary>
        /// <param name="callbackOnChanged">Callback to invoke on revision changes</param>
        public abstract void SetCallbacks(Action<IComponentRefCore, ulong> callbackOnChanged);
    }

    /// <summary>
    /// Stores components of type TComp in the world.
    /// This is a concrete implementation of ComponentStore that manages components of a specific type.
    /// </summary>
    /// <typeparam name="TComp">The type of component to manage</typeparam>
    public sealed class ComponentStore<TComp> : ComponentStore where TComp : struct, IComponent<TComp>
    {
        /// <summary>
        /// Represents a group containing a component, its reference core, entity ID, and version.
        /// This is the internal storage structure for components.
        /// </summary>
        public struct Group
        {
            /// <summary>
            /// The component data.
            /// </summary>
            public TComp Component;

            /// <summary>
            /// The reference core for this component.
            /// </summary>
            public ComponentRefCore RefCore;

            /// <summary>
            /// The ID of the entity that owns this component.
            /// </summary>
            public ulong Entity;

            /// <summary>
            /// The version of this component.
            /// </summary>
            public uint Version;

            /// <summary>
            /// The modification revision of this component.
            /// </summary>
            public uint Revision;
        }
        
        /// <summary>
        /// Implementation of IComponentRefLocator for this component store.
        /// Provides methods to access component data and metadata.
        /// </summary>
        private class Locator : IComponentRefLocator
        {
            /// <summary>
            /// Reference to the component store.
            /// </summary>
            private readonly ComponentStore<TComp> m_store;
            
            /// <summary>
            /// Checks if a component reference at the specified offset is valid and not null.
            /// </summary>
            /// <param name="version">Version of the component reference to verify</param>
            /// <param name="offset">Memory offset of the component reference</param>
            /// <returns>True if the component reference is valid and not null, false otherwise</returns>
            public bool NotNull(uint version, int offset)
            {
                if (offset >= m_store.Allocated) return false;
                ref var g = ref m_store.m_components[offset];
                
                return g.Entity != 0 && g.Version == version;
            }

            /// <summary>
            /// Gets a reference to the component data of type T at the specified offset.
            /// </summary>
            /// <typeparam name="T">Component type to get</typeparam>
            /// <param name="offset">Memory offset of the component</param>
            /// <returns>Reference to the component data</returns>
            public ref T Get<T>(int offset) where T : struct, IComponent<T>
            {
#if NET6_0_OR_GREATER
                return ref Unsafe.As<TComp, T>(ref m_store.m_components[offset].Component);
#else
                unsafe
                {
                    // Get pointer to the component
                    fixed (TComp* componentPtr = &m_store.m_components[offset].Component)
                    {
                        // Cast pointer to T*
                        T* tPtr = (T*)componentPtr;
                        // Return reference to the value
                        return ref *tPtr;
                    }
                }
#endif
            }

            /// <summary>
            /// Checks if the component at the specified offset is of the given type.
            /// </summary>
            /// <param name="type">Type to check against</param>
            /// <returns>True if the component is of the specified type, false otherwise</returns>
            public bool IsT(Type type)
            {
                return type == typeof(TComp);
            }

            /// <summary>
            /// Gets the actual runtime type of the component at the specified offset.
            /// </summary>
            /// <returns>The runtime type of the component</returns>
            public Type GetT()
            {
                return typeof(TComp);
            }

            /// <summary>
            /// Gets the entity ID associated with the component at the specified offset.
            /// </summary>
            /// <param name="offset">Memory offset of the component</param>
            /// <returns>Entity ID that owns this component</returns>
            public ulong GetEntityId(int offset)
            {
                if (offset >= m_store.Allocated) return 0;
                ref var gs = ref m_store.m_components[offset];

                return gs.Entity;
            }

            /// <summary>
            /// Gets the core reference object for the component at the specified offset.
            /// </summary>
            /// <param name="offset">Memory offset of the component</param>
            /// <returns>Core reference object containing locator and offset information</returns>
            public IComponentRefCore GetRefCore(int offset)
            {
                if (offset >= m_store.Allocated) return null;
                ref var gs = ref m_store.m_components[offset];
                if (gs.Entity == 0) return null;

                return gs.RefCore;
            }

            /// <summary>
            /// Gets the modification revision of the component at the specified offset.
            /// </summary>
            /// <param name="offset">Memory offset of the component</param>
            /// <returns>Modification revision of the component</returns>
            /// <exception cref="NotImplementedException"></exception>
            public uint GetRevision(int offset)
            {
                if (offset >= m_store.Allocated) return 0;
                ref var gs = ref m_store.m_components[offset];
                if (gs.Entity == 0) return 0;

                return gs.Revision;
            }

            /// <summary>
            /// Changes the modification revision of the component at the specified offset.
            /// </summary>
            /// <param name="offset">Memory offset of the component</param>
            /// <returns>New modification revision of the component</returns>
            public uint ChangeRevision(int offset)
            {
                if (offset >= m_store.Allocated) return 0;
                ref var gs = ref m_store.m_components[offset];
                if (gs.Entity == 0) return 0;

                gs.Revision = (gs.Revision % uint.MaxValue) + 1;
                m_store.m_revisionChanged?.Invoke(gs.RefCore, gs.Entity);
                return gs.Revision;
            }

            /// <summary>
            /// Initializes a new instance of the Locator class.
            /// </summary>
            /// <param name="store">The component store to locate components in</param>
            public Locator(ComponentStore<TComp> store)
            {
                m_store = store;
            }
        }

        /// <summary>
        /// The locator for this component store.
        /// </summary>
        private Locator m_locator;

        /// <summary>
        /// Callback invoked when a component revision changes.
        /// </summary>
        private Action<IComponentRefCore, ulong> m_revisionChanged;
        
        /// <summary>
        /// Array of component groups containing component data and metadata.
        /// </summary>
        private Group[] m_components;

        /// <summary>
        /// Gets the reference locator of this store.
        /// </summary>
        public override IComponentRefLocator RefLocator
        {
            get
            {
                if (m_locator == null) m_locator = new Locator(this);
                return m_locator;
            }
        }

        /// <summary>
        /// Gets the array of component groups.
        /// This provides direct access to the underlying component storage.
        /// </summary>
        public Group[] ComponentGroups => m_components;

        /// <summary>
        /// Gets the number of allocated components in this store.
        /// </summary>
        public int Allocated { get; private set; }

        /// <summary>
        /// Gets the capacity of this store.
        /// Reads directly from ComponentGroups.
        /// </summary>
        public int Capacity => m_components.Length;

        /// <summary>
        /// Gets or sets the auto-increase rate for the component store.
        /// Determines how much the store capacity increases when it needs to expand.
        /// </summary>
        public float AutoIncreaseRate;

        /// <summary>
        /// Gets or sets the auto-increase trigger edge for the component store.
        /// Determines when the store should expand based on its current capacity usage.
        /// </summary>
        public float AutoIncreaseTriggerEdge;
        
        /// <summary>
        /// Allocates a new component in this store for the specified entity.
        /// </summary>
        /// <param name="entityId">The ID of the entity that will own the component</param>
        /// <returns>The position of the allocated component in the store</returns>
        public override int Fix(ulong entityId)
        {
            return Fix(entityId, default);
        }
        
        /// <summary>
        /// Initializes a new instance of the ComponentManager class.
        /// </summary>
        /// <param name="entityId">The ID of the entity that will own the component</param>
        /// <param name="initialValue">The initial value of the component</param>
        /// <returns>The position of the allocated component in the store</returns>
        public int Fix(ulong entityId, TComp initialValue)
        {
            const int requiredSlots = 1;
            var pos = Allocated;
            var capa = m_components.Length;
            
            // Check if we need to expand the array
            if (NeedsMoreSpace(pos, capa))
            {
                var compactedCount = 0;
                if (NeedRearrange)
                {
                    compactedCount = Rearrange();
                    pos = Allocated;
                }

                if (compactedCount < requiredSlots)
                {
                    capa = m_components.Length;
                    var newSize = (int) MathF.Floor(MathF.Max(pos + 1, MathF.Round(capa * AutoIncreaseRate)));
                    Array.Resize(ref m_components, newSize);
                }
            }

            ref var gs = ref m_components[pos];
            var version = gs.Version;
            gs = default;
            gs.Component = initialValue;
            gs.Entity = entityId;
            gs.Version = (version % uint.MaxValue) + 1;

            gs.RefCore = ComponentRefCore.Pool.Get();
            gs.RefCore.Allocate(RefLocator, pos, gs.Version);
            Allocated += 1;

            try
            {
                gs.Component.OnCreate(entityId);
            }
            catch (Exception e)
            {
                Log.Exp(e);
            }
            
            return pos;

            bool NeedsMoreSpace(int position, int capacity)
            {
                return position > MathF.Floor(capacity * AutoIncreaseTriggerEdge) || position >= capacity;
            }
        }

        /// <summary>
        /// Releases a component in this store at the specified position.
        /// Marks the component slot as invalid so it can be compacted on the next rearrange.
        /// </summary>
        /// <param name="pos">The position of the component to release</param>
        /// <returns>True if the component was successfully released, false otherwise</returns>
        public override bool Release(int pos)
        {
            if (pos < 0 || pos >= Allocated) return false;
            
            ref var posGs = ref m_components[pos];
            if (posGs.RefCore == null) return false;
            
            try
            {
                posGs.Component.OnDestroy(posGs.Entity);
            }
            catch (Exception e)
            {
                Log.Exp(e);
            }

            posGs.Revision = 0;
            posGs.Entity = 0;
            ComponentRefCore.Pool.Release(posGs.RefCore);
            posGs.RefCore = null;
            NeedRearrange = true;
            return true;
        }

        /// <summary>
        /// Rearranges the components in this store to optimize memory usage.
        /// </summary>
        /// <returns>The number of invalid component slots removed from the allocated range.</returns>
        public override int Rearrange()
        {
            var oldAllocated = Allocated;
            var left = 0;
            var right = oldAllocated - 1;

            while (left <= right)
            {
                while (left <= right && m_components[left].Entity != 0)
                    left += 1;

                while (left <= right && m_components[right].Entity == 0)
                    right -= 1;

                if (left > right) break;

                m_components[left] = m_components[right];
                ref var gs = ref m_components[left];
                gs.RefCore?.Relocate(left);
                left += 1;
                right -= 1;
            }

            Allocated = right + 1;
            NeedRearrange = false;
            return oldAllocated - Allocated;
        }

        /// <summary>
        /// Sets the callbacks used by this store.
        /// </summary>
        /// <param name="callbackOnChanged">Callback to invoke on revision changes</param>
        public override void SetCallbacks(Action<IComponentRefCore, ulong> callbackOnChanged)
        {
            m_revisionChanged = callbackOnChanged;
        }

        /// <summary>
        /// Expands the capacity of this store by the specified count.
        /// </summary>
        /// <param name="count">The number of additional slots to add</param>
        /// <returns>The actual number of slots added</returns>
        public int Expand(int count)
        {
            var realCount = Math.Max(0, count);
            
            Array.Resize(ref m_components, m_components.Length + realCount);

            return realCount;
        }

        /// <summary>
        /// Initializes a new instance of the ComponentStore class with the specified parameters.
        /// </summary>
        /// <param name="initialSize">Initial capacity of the store</param>
        /// <param name="autoIncreaseRate">Rate at which the store expands when needed</param>
        /// <param name="autoIncreaseTriggerEdge">Trigger point for expanding the store</param>
        public ComponentStore(int initialSize = 100, float autoIncreaseRate = 2, float autoIncreaseTriggerEdge = 1.2f)
        {
            m_components = new Group[initialSize];
            AutoIncreaseRate = autoIncreaseRate;
            AutoIncreaseTriggerEdge = autoIncreaseTriggerEdge;
        }
        
        /// <summary>
        /// Initializes a new instance of the ComponentStore class with default parameters.
        /// </summary>
        public ComponentStore()
        {
            m_components = new Group[100];
            AutoIncreaseRate = 2;
            AutoIncreaseTriggerEdge = 1.2f;
        }
    }
    
    /// <summary>
    /// Manages components in the world.
    /// This class is responsible for creating, destroying, and organizing component stores.
    /// </summary>
    public sealed class ComponentManager : IWorldManager
    {
        /// <summary>
        /// Emitter for component creation events.
        /// </summary>
        private static readonly Emitter<ComponentCreated, IComponentRefCore, ulong, Type> _addEmitter = 
            (h, a, b, c) => h(a, b, c);
        
        
        /// <summary>
        /// Emitter for component destruction events.
        /// </summary>
        private static readonly Emitter<ComponentDestroyed, IComponentRefCore, ulong, Type> _rmEmitter = 
            (h, a, b, c) => h(a, b, c);

        /// <summary>
        /// Emitter for component revision change events.
        /// </summary>
        private static readonly Emitter<ComponentChanged, IComponentRefCore, ulong, Type> _changeEmitter =
            (h, a, b, c) => h(a, b, c);
        
        /// <summary>
        /// Dictionary mapping component types to their stores.
        /// </summary>
        private readonly Dictionary<Type, ComponentStore> m_compStores = new();

        /// <summary>
        /// Cached delegate for revision change callbacks.
        /// </summary>
        private readonly Action<IComponentRefCore, ulong> m_onComponentChangedCallback;

        /// <summary>
        /// Registry of dense archetypes and their chunk storage.
        /// </summary>
        private readonly ArchetypeRegistry m_registry;

        /// <summary>
        /// Resolves an entity id into its <see cref="EntityGraph"/>; bound by the entity manager.
        /// </summary>
        private Func<ulong, EntityGraph> m_graphResolver;

        /// <summary>
        /// Event triggered when a component is created.
        /// </summary>
        public Signal<ComponentCreated> OnComponentCreated { get; } = new();

        /// <summary>
        /// Event triggered when a component is removed.
        /// </summary>
        public Signal<ComponentDestroyed> OnComponentRemoved { get; } = new();

        /// <summary>
        /// Event triggered when a component revision changes.
        /// </summary>
        public Signal<ComponentChanged> OnComponentChanged { get; } = new();

        /// <summary>
        /// Gets all component stores in this manager.
        /// </summary>
        /// <returns>An enumerable of all component stores</returns>
        public IEnumerable<ComponentStore> GetAllComponentStores()
        {
            return m_compStores.Values;
        }
        
        /// <summary>
        /// Gets the component store for the specified component type.
        /// </summary>
        /// <typeparam name="TComp">The component type</typeparam>
        /// <param name="createIfNotExist">Whether to create the store if it doesn't exist</param>
        /// <returns>The component store for the specified type, or null if not found and createIfNotExist is false</returns>
        public ComponentStore<TComp> GetComponentStore<TComp>(bool createIfNotExist = true) 
            where TComp : struct, IComponent<TComp>
        {
            if (m_compStores.TryGetValue(typeof(TComp), out var store))
            {
                if (store is not ComponentStore<TComp> r) throw new InvalidCastException();
                return r;
            }

            if (!createIfNotExist) return null;

            var ns = new ComponentStore<TComp>();
            ns.SetCallbacks(m_onComponentChangedCallback);
            m_compStores.Add(typeof(TComp), ns);
            return ns;
        }
        
        /// <summary>
        /// Gets the component store for the specified component type.
        /// </summary>
        /// <param name="type">The component type</param>
        /// <param name="createIfNotExist">Whether to create the store if it doesn't exist</param>
        /// <returns>The component store for the specified type, or null if not found and createIfNotExist is false</returns>
        public ComponentStore GetComponentStore(Type type, bool createIfNotExist = true) 
        {
            var storeType = typeof(ComponentStore<>).MakeGenericType(type);
            if (m_compStores.TryGetValue(type, out var store))
            {
                return store;
            }

            if (!createIfNotExist) return null;
            
            var ns = (ComponentStore) Activator.CreateInstance(storeType);
            ns.SetCallbacks(m_onComponentChangedCallback);
            m_compStores.Add(type, ns);
            
            return ns;
        }

        /// <summary>
        /// Handles component revision change events.
        /// </summary>
        /// <param name="core">Changed component reference</param>
        /// <param name="entityId">Entity that owns the component</param>
        private void _onComponentChanged(IComponentRefCore core, ulong entityId)
        {
            OnComponentChanged.Emit(core, entityId, core.RefLocator.GetT(), _changeEmitter);
        }

        /// <summary>
        /// Creates a new component of type T for the specified entity.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entityId">The ID of the entity that will own the component</param>
        /// <returns>The core reference for the created component</returns>
        public IComponentRefCore CreateComponent<T>(ulong entityId) where T : struct, IComponent<T>
        {
            var graph = ResolveGraph(entityId);
            return ComponentStorageKind.IsSparse<T>()
                ? CreateSparse<T>(graph, entityId, false, default)
                : CreateDense<T>(graph, entityId, false, default);
        }
        
        /// <summary>
        /// Creates a new component of type T for the specified entity.
        /// </summary>
        /// <typeparam name="T">The component type</typeparam>
        /// <param name="entityId">The ID of the entity that will own the component</param>
        /// <param name="initialValue">The initial value for the component</param>
        /// <returns>The core reference for the created component</returns>
        public IComponentRefCore CreateComponent<T>(ulong entityId, T initialValue) where T : struct, IComponent<T>
        {
            var graph = ResolveGraph(entityId);
            return ComponentStorageKind.IsSparse<T>()
                ? CreateSparse<T>(graph, entityId, true, initialValue)
                : CreateDense<T>(graph, entityId, true, initialValue);
        }

        /// <summary>
        /// Creates a dense component instance by migrating the entity to an archetype with one more instance of <typeparamref name="T"/>.
        /// </summary>
        private IComponentRefCore CreateDense<T>(EntityGraph graph, ulong entityId, bool hasInitial, T initialValue)
            where T : struct, IComponent<T>
        {
            var sourceArchetype = m_registry.Get(graph.ArchetypeId);
            sourceArchetype.Locate(graph.Row, out var sourceChunk, out var sourceLocal);

            var destSignature = AddOneComponent(sourceArchetype.Signature, typeof(T), out var newInstanceIndex);
            var destArchetype = m_registry.GetOrCreate(destSignature);

            // Both source and destination must be free of read locks before any mutation begins.
            TryBeginDenseStructuralChange(sourceArchetype);
            TryBeginDenseStructuralChange(destArchetype);

            var sourceGlobalRow = graph.Row;
            var destGlobal = destArchetype.AddEntityRow(entityId, out var destChunk, out var destLocal);

            sourceChunk.MigrateRowInto(destChunk, sourceLocal, destLocal, null, 0);

            // Update the graph location to the destination row before OnCreate runs so the hook
            // observes the entity at its new archetype/row (with surviving components already in place).
            graph.ArchetypeId = destArchetype.Id;
            graph.Row = destGlobal;

            var core = destChunk.CreateDense<T>(destLocal, newInstanceIndex, entityId, hasInitial, initialValue);

            var moved = sourceArchetype.RemoveEntityRow(sourceGlobalRow, out var movedNewGlobalRow);
            if (moved != 0)
            {
                var movedGraph = m_graphResolver?.Invoke(moved);
                if (movedGraph != null) movedGraph.Row = movedNewGlobalRow;
            }

            OnComponentCreated.Emit(core, entityId, typeof(T), _addEmitter);
            return core;
        }

        /// <summary>
        /// Creates a sparse component instance in the backing <see cref="ComponentStore{T}"/> and records the handle in the entity's row proxy.
        /// Sparse components never change the entity's archetype.
        /// </summary>
        private IComponentRefCore CreateSparse<T>(EntityGraph graph, ulong entityId, bool hasInitial, T initialValue)
            where T : struct, IComponent<T>
        {
            var store = GetComponentStore<T>();
            var allocComp = hasInitial ? store.Fix(entityId, initialValue) : store.Fix(entityId);
            var core = store.RefLocator.GetRefCore(allocComp);

            var archetype = m_registry.Get(graph.ArchetypeId);
            archetype.Locate(graph.Row, out var chunk, out var local);
            chunk.Proxies[local].Add(core);

            OnComponentCreated.Emit(core, entityId, typeof(T), _addEmitter);
            return core;
        }

        /// <summary>
        /// Destroys a component, routing sparse components to the store and dense components to chunk migration.
        /// </summary>
        /// <param name="core">The core reference of the component to destroy</param>
        public void DestroyComponent(IComponentRefCore core)
        {
            if (core.RefLocator == null)
                throw new InvalidOperationException("Component has already been destroyed!");

            var compType = core.RefLocator.GetT();
            if (ComponentStorageKind.IsSparse(compType))
                DestroySparse(core, compType);
            else
                DestroyDense(core, compType);
        }

        private void DestroySparse(IComponentRefCore core, Type compType)
        {
            var idx = core.Offset;
            var store = GetComponentStore(compType);
            var entityId = store.RefLocator.GetEntityId(idx);

            var graph = m_graphResolver?.Invoke(entityId);
            if (graph != null && graph.Row >= 0)
            {
                var archetype = m_registry.Get(graph.ArchetypeId);
                archetype.Locate(graph.Row, out var chunk, out var local);
                chunk.Proxies[local]?.Remove(core);
            }

            if (store.Release(idx)) OnComponentRemoved.Emit(core, entityId, compType, _rmEmitter);
        }

        private void DestroyDense(IComponentRefCore core, Type compType)
        {
            var entityId = core.RefLocator.GetEntityId(core.Offset);
            var graph = ResolveGraph(entityId);

            var sourceArchetype = m_registry.Get(graph.ArchetypeId);
            sourceArchetype.Locate(graph.Row, out var sourceChunk, out var sourceLocal);

            if (!sourceChunk.TryGetDenseSlot(sourceLocal, core, out _, out var instanceIndex))
                throw new InvalidOperationException("Component reference is not attached to its entity's chunk row.");

            var destSignature = RemoveOneComponent(sourceArchetype.Signature, compType);
            var destArchetype = m_registry.GetOrCreate(destSignature);

            // Both source and destination must be free of read locks before any mutation begins.
            TryBeginDenseStructuralChange(sourceArchetype);
            TryBeginDenseStructuralChange(destArchetype);

            var sourceGlobalRow = graph.Row;

            // Run OnDestroy while the entity is still at its (valid) source location, before survivors relocate.
            sourceChunk.DestroyDenseSlot(sourceLocal, compType, instanceIndex);

            var destGlobal = destArchetype.AddEntityRow(entityId, out var destChunk, out var destLocal);
            sourceChunk.MigrateRowInto(destChunk, sourceLocal, destLocal, compType, instanceIndex);

            var moved = sourceArchetype.RemoveEntityRow(sourceGlobalRow, out var movedNewGlobalRow);
            if (moved != 0)
            {
                var movedGraph = m_graphResolver?.Invoke(moved);
                if (movedGraph != null) movedGraph.Row = movedNewGlobalRow;
            }

            graph.ArchetypeId = destArchetype.Id;
            graph.Row = destGlobal;

            OnComponentRemoved.Emit(core, entityId, compType, _rmEmitter);
        }
        
        public void CleanupComponents()
        {
            foreach (var store in m_compStores.Values)
                if (store.NeedRearrange)
                    store.Rearrange();
        }

        /// <summary>
        /// Initializes a new instance of the ComponentManager class.
        /// </summary>
        public ComponentManager()
        {
            m_onComponentChangedCallback = _onComponentChanged;
            m_registry = new ArchetypeRegistry(m_onComponentChangedCallback);
        }

        /// <summary>
        /// Binds the entity graph resolver used to locate an entity's archetype/row during component mutation.
        /// </summary>
        /// <param name="resolver">Delegate mapping entity id to its <see cref="EntityGraph"/> (null when absent).</param>
        internal void BindEntityGraphResolver(Func<ulong, EntityGraph> resolver)
        {
            m_graphResolver = resolver;
        }

        /// <summary>
        /// Places a freshly created entity in the empty (proxy-only) archetype and records its location.
        /// </summary>
        /// <param name="graph">Entity graph to place.</param>
        internal void PlaceNewEntity(EntityGraph graph)
        {
            var empty = m_registry.Empty;
            var global = empty.AddEntityRow(graph.EntityId, out _, out _);
            graph.ArchetypeId = empty.Id;
            graph.Row = global;
        }

        /// <summary>
        /// Removes the entity's chunk row (after all of its components were released) and clears its location.
        /// </summary>
        /// <param name="graph">Entity graph to remove.</param>
        internal void RemoveEntityRow(EntityGraph graph)
        {
            if (graph.Row < 0) return;

            var archetype = m_registry.Get(graph.ArchetypeId);
            var moved = archetype.RemoveEntityRow(graph.Row, out var movedNewGlobalRow);
            if (moved != 0)
            {
                var movedGraph = m_graphResolver?.Invoke(moved);
                if (movedGraph != null) movedGraph.Row = movedNewGlobalRow;
            }

            graph.ArchetypeId = 0;
            graph.Row = -1;
        }

        /// <summary>
        /// Verifies that a dense structural change is permitted on <paramref name="archetype"/>.
        /// </summary>
        /// <param name="archetype">Archetype about to be mutated.</param>
        /// <returns>True when the change may proceed immediately.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the archetype is read-locked.</exception>
        internal bool TryBeginDenseStructuralChange(Archetype archetype)
        {
            if (archetype.IsReadLocked)
                throw new InvalidOperationException(
                    "Cannot perform a dense structural change while the archetype is read-locked; use a CommandBuffer to defer the change.");

            return true;
        }

        /// <summary>
        /// Resolves the archetype currently hosting <paramref name="entityId"/>'s dense storage row.
        /// </summary>
        /// <param name="entityId">Entity whose archetype to resolve.</param>
        /// <returns>The archetype the entity is currently placed in.</returns>
        internal Archetype GetEntityArchetype(ulong entityId)
        {
            var graph = ResolveGraph(entityId);
            return m_registry.Get(graph.ArchetypeId);
        }

        /// <summary>
        /// Resolves the dense signature and sparse proxy used to evaluate entity matchers.
        /// </summary>
        /// <param name="entityId">Entity whose match inputs to resolve.</param>
        /// <param name="signature">Dense component composition for the entity's archetype row.</param>
        /// <param name="sparseProxy">Sparse component handles attached to the entity row, or null when unplaced.</param>
        internal void GetEntityMatchInputs(ulong entityId, out ArchetypeSignature signature, out SparseSetProxy sparseProxy)
        {
            var graph = ResolveGraph(entityId);
            var archetype = m_registry.Get(graph.ArchetypeId);
            signature = archetype.Signature;
            if (graph.Row < 0)
            {
                sparseProxy = null;
                return;
            }

            archetype.Locate(graph.Row, out var chunk, out var local);
            sparseProxy = chunk.Proxies[local];
        }

        private EntityGraph ResolveGraph(ulong entityId)
        {
            var graph = m_graphResolver?.Invoke(entityId);
            if (graph == null)
                throw new InvalidOperationException($"Entity {entityId} does not exist or has no archetype location.");

            return graph;
        }

        private static ArchetypeSignature AddOneComponent(ArchetypeSignature signature, Type type, out int newInstanceIndex)
        {
            var entries = signature.Entries;
            var result = new List<ArchetypeEntry>(entries.Count + 1);
            var found = false;
            newInstanceIndex = 0;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == type)
                {
                    newInstanceIndex = entry.Count;
                    result.Add(new ArchetypeEntry(type, entry.Count + 1));
                    found = true;
                }
                else
                {
                    result.Add(entry);
                }
            }

            if (!found)
            {
                newInstanceIndex = 0;
                result.Add(new ArchetypeEntry(type, 1));
            }

            return ArchetypeSignature.From(result.ToArray());
        }

        private static ArchetypeSignature RemoveOneComponent(ArchetypeSignature signature, Type type)
        {
            var entries = signature.Entries;
            var result = new List<ArchetypeEntry>(entries.Count);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == type)
                {
                    if (entry.Count > 1)
                        result.Add(new ArchetypeEntry(type, entry.Count - 1));
                }
                else
                {
                    result.Add(entry);
                }
            }

            return result.Count == 0 ? ArchetypeSignature.Empty : ArchetypeSignature.From(result.ToArray());
        }

        /// <summary>
        /// Called when the manager is created.
        /// </summary>
        public void OnManagerCreated() {}

        /// <summary>
        /// Called when the world starts.
        /// </summary>
        public void OnWorldStarted() {}

        /// <summary>
        /// Called when the world ends.
        /// </summary>
        public void OnWorldEnded() {}

        /// <summary>
        /// Called when the manager is destroyed.
        /// </summary>
        public void OnManagerDestroyed() {}
    }
}