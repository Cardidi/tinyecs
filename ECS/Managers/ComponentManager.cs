using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TinyECS.Defines;
using TinyECS.Utils;

namespace TinyECS.Managers
{

    public delegate void ComponentCreated(ComponentRefCore component, ulong entityId);

    public delegate void ComponentDestroyed(ComponentRefCore component, ulong entityId);

    public abstract class ComponentStore
    {
        /// <summary>
        /// Get reference locator of this store.
        /// </summary>
        public abstract IComponentRefLocator RefLocator { get; }
        
        /// <summary>
        /// Get all reference cores in this store.
        /// </summary>
        public abstract IEnumerable<ComponentRefCore> Cores { get; }

        /// <summary>
        /// Allocate a component in this store.
        /// </summary>
        public abstract int Increase(ulong entityId);

        /// <summary>
        /// Release a component in this store.
        /// </summary>
        public abstract bool Decrease(int pos);

    }

    public sealed class ComponentStore<TComp> : ComponentStore where TComp : struct, IComponent<TComp>
    {
        
        public struct Group
        {
            public TComp Component;

            public ComponentRefCore RefCore;

            public ulong Entity;

            public uint Version;
        }
        
        private class Locator : IComponentRefLocator
        {
            private readonly ComponentStore<TComp> m_store;
            
            public bool NotNull(uint version, int offset)
            {
                if (offset >= m_store.Allocated) return false;
                ref var g = ref m_store.m_components[offset];
                
                return g.Version == version;
            }

            public ref T Get<T>(int offset) where T : struct, IComponent<T>
            {
                return ref Unsafe.As<TComp, T>(ref m_store.m_components[offset].Component);
            }

            public bool IsT(Type type)
            {
                return type == typeof(TComp);
            }

            public Type GetT()
            {
                return typeof(TComp);
            }

            public ulong GetEntityId(int offset)
            {
                if (offset >= m_store.Allocated) return 0;
                ref var gs = ref m_store.m_components[offset];

                return gs.Entity;
            }

            public ComponentRefCore GetRefCore(int offset)
            {
                if (offset >= m_store.Allocated) return null;
                ref var gs = ref m_store.m_components[offset];

                return gs.RefCore;
            }

            public Locator(ComponentStore<TComp> store)
            {
                m_store = store;
            }
        }

        private Locator m_locator;
        
        private Group[] m_components;

        public override IComponentRefLocator RefLocator
        {
            get
            {
                if (m_locator == null) m_locator = new Locator(this);
                return m_locator;
            }
        }
        
        public override IEnumerable<ComponentRefCore> Cores
            => m_components.Take(Allocated).Select(x => x.RefCore);

        /// <summary>
        /// Memory strip of components.
        /// </summary>
        public Group[] ComponentGroups => m_components;

        /// <summary>
        /// Note of allocated components.
        /// </summary>
        public int Allocated { get; private set; }

        /// <summary>
        /// Capacities of store. Reads directly from <see cref="ComponentGroups"/>.
        /// </summary>
        public int Capacity => m_components.Length;

        public float AutoIncreaseRate;

        public float AutoIncreaseTriggerEdge;
        
        public override int Increase(ulong entityId)
        {
            var pos = Allocated;
            var capa = m_components.Length;
            if (pos > MathF.Floor(capa * AutoIncreaseTriggerEdge) || pos >= capa)
            {
                var newSize = (int) MathF.Floor(MathF.Max(pos + 1, MathF.Round(capa * AutoIncreaseRate)));
                Array.Resize(ref m_components, newSize);
            }

            ref var gs = ref m_components[pos];
            gs.Component = default;
            gs.Entity = entityId;
            gs.Version += 1;

            gs.RefCore = new ComponentRefCore(RefLocator, pos, gs.Version);
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
        }

        public override bool Decrease(int pos)
        {
            if (pos < 0 || pos >= Allocated) return false;
            var swap = Allocated - 1;
            var canSwap = pos < swap;

            ref var posGs = ref m_components[pos];
            
            try
            {
                posGs.Component.OnDestroy(posGs.Entity);
            }
            catch (Exception e)
            {
                Log.Exp(e);
            }
            
            if (canSwap)
            {
                ref var swapGs = ref m_components[pos];

                posGs.Entity = swapGs.Entity;
                posGs.Version = swapGs.Version;
                
                
                posGs.RefCore.RewriteRef(null, -1, 0);
                posGs.RefCore = swapGs.RefCore;
                swapGs.RefCore = null;
                posGs.RefCore.RewriteRef(RefLocator, pos, posGs.Version);

            }
            else
            {
                posGs.Entity = 0;
                posGs.RefCore.RewriteRef(null, -1, 0);
                posGs.RefCore = null; // Do not refer it to avoid memory leak.
            }

            Allocated -= 1;
            return true;
        }

        public int Expand(int count)
        {
            var realCount = Math.Max(0, count);
            
            Array.Resize(ref m_components, m_components.Length + realCount);

            return realCount;
        }

        public ComponentStore(int initialSize = 100, float autoIncreaseRate = 2, float autoIncreaseTriggerEdge = 1.2f)
        {
            m_components = new Group[initialSize];
            AutoIncreaseRate = autoIncreaseRate;
            AutoIncreaseTriggerEdge = autoIncreaseTriggerEdge;
        }
    }


    public sealed class ComponentManager : IWorldManager
    {
        private static readonly Emitter<ComponentCreated, ComponentRefCore, ulong> _addEmitter = 
            (h, a, b) => h(a, b);
        
        
        private static readonly Emitter<ComponentDestroyed, ComponentRefCore, ulong> _rmEmitter = 
            (h, a, b) => h(a, b);
        
        public IWorld World { get; private set; }
        
        private readonly Dictionary<Type, ComponentStore> m_compStores = new();

        public Signal<ComponentCreated> OnComponentCreated { get; } = new();

        public Signal<ComponentDestroyed> OnComponentRemoved { get; } = new();


        public IEnumerable<ComponentStore> GetAllComponentStores()
        {
            return m_compStores.Values;
        }
        
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
            m_compStores.Add(typeof(TComp), ns);
            return ns;
        }
        
        public ComponentStore GetComponentStore(Type type, bool createIfNotExist = true) 
        {
            if (m_compStores.TryGetValue(type, out var store))
            {
                return store;
            }

            if (!createIfNotExist) return null;

            var storeType = typeof(ComponentStore<>).MakeGenericType(type);
            var ns = (ComponentStore) Activator.CreateInstance(storeType);
            m_compStores.Add(storeType, ns);
            
            return ns;
        }

        public ComponentRefCore CreateComponent<T>(ulong entityId) where T : struct, IComponent<T>
        {
            var store = GetComponentStore<T>();

            var allocComp = store.Increase(entityId);
            var core = store.RefLocator.GetRefCore(allocComp);
            
            OnComponentCreated.Emit(core, entityId, _addEmitter);
            return core;
        }

        public ComponentRefCore CreateComponent(ulong entityId, Type type)
        {
            var store = GetComponentStore(type);

            var allocComp = store.Increase(entityId);
            var core = store.RefLocator.GetRefCore(allocComp);
            
            OnComponentCreated.Emit(core, entityId, _addEmitter);
            return core;
        }

        public void DestroyComponent<T>(ComponentRefCore core) where T : struct, IComponent<T>
        {
            if (core.RefLocator == null)
                throw new InvalidOperationException("Component has already been destroyed!");
            
            if (!core.RefLocator.IsT(typeof(T)))
                throw new InvalidOperationException("Component type is unmatched!");

            var idx = core.Offset;
            var store = GetComponentStore<T>();
            var entityId = store.ComponentGroups[idx].Entity;
            
            if (store.Decrease(idx)) OnComponentRemoved.Emit(core, entityId, _rmEmitter);
        }

        public void DestroyComponent(ComponentRefCore core)
        {
            if (core.RefLocator == null)
                throw new InvalidOperationException("Component has already been destroyed!");

            var idx = core.Offset;
            var store = GetComponentStore(core.RefLocator.GetT());
            var entityId = store.RefLocator.GetEntityId(idx);
            
            if (store.Decrease(idx)) OnComponentRemoved.Emit(core, entityId, _rmEmitter);
        }

        public void OnManagerCreated() {}

        public void OnWorldStarted() {}

        public void OnWorldEnded() {}

        public void OnManagerDestroyed() {}
    }
}