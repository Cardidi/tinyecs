using TinyECS.Vendor;

namespace TinyECS
{

    public delegate void CompAddedHandler(ComponentRefCore component, ulong entityId);

    public delegate void CompRemovedHandler(ComponentRefCore component, ulong entityId);


    public sealed class ComponentPlugin<TWorld> : IPlugin<TWorld> where TWorld : class, IWorld<TWorld>
    {
        private static readonly Emitter<CompAddedHandler, ComponentRefCore, ulong> _addEmitter = 
            (h, a, b) => h(a, b);
        
        
        private static readonly Emitter<CompRemovedHandler, ComponentRefCore, ulong> _rmEmitter = 
            (h, a, b) => h(a, b);

        #region DataArea

        private readonly Dictionary<Type, ComponentStore> m_compStores = new();

        #endregion

        #region World Modification Events

        public Signal<CompAddedHandler> OnCompAdded { get; } = new();

        public Signal<CompRemovedHandler> OnCompRemoved { get; } = new();

        #endregion

        public void OnConstruct(TWorld world,
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyList<ISystem<TWorld>> systems,
            IReadOnlyDictionary<object, object> envData)
        {
            
        }

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
            
            OnCompAdded.Emit(core, entityId, _addEmitter);
            return core;
        }

        public ComponentRefCore CreateComponent(Type type, ulong entityId)
        {
            var store = GetComponentStore(type);

            var allocComp = store.Increase(entityId);
            var core = store.RefLocator.GetRefCore(allocComp);
            
            OnCompAdded.Emit(core, entityId, _addEmitter);
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
            
            if (store.Decrease(idx)) OnCompRemoved.Emit(core, entityId, _rmEmitter);
        }

        public void DestroyComponent(ComponentRefCore core)
        {
            if (core.RefLocator == null)
                throw new InvalidOperationException("Component has already been destroyed!");

            var idx = core.Offset;
            var store = GetComponentStore(core.RefLocator.GetT());
            var entityId = store.RefLocator.GetEntityId(idx);
            
            if (store.Decrease(idx)) OnCompRemoved.Emit(core, entityId, _rmEmitter);
        }
    }
}