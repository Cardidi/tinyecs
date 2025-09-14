// ReSharper disable ForCanBeConvertedToForeach

using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using TinyECS.Core;
using Object = System.Object;

namespace TinyECS.Core {

    /// <summary>
    /// World builder. It can be cached and await to create any new world.
    /// </summary>
    /// <typeparam name="TWorld">Created world type</typeparam>
    public sealed class WorldBuilder<TWorld> where TWorld : World<TWorld>
    {
        private static readonly Type SystemType = typeof(ISystem<TWorld>);
        
        private static readonly Type PluginType = typeof(IPlugin<TWorld>);

        private static readonly MethodInfo PostCtorInjection =
            typeof(World<TWorld>).GetMethod("_postCtor", BindingFlags.Instance |  BindingFlags.NonPublic);
        
        private readonly List<SortedObject<Type>> m_registerSystems = new();

        private readonly List<SortedObject<Type>> m_registerPlugins = new()
        {
            new (0, typeof(EntityPlugin<TWorld>)),
            new (0, typeof(SystemPlugin<TWorld>)),
            new (0, typeof(ComponentPlugin<TWorld>))
        };

        private readonly Dictionary<object, object> m_envData = new();
        
        private WorldBuilder() {}

        private void AddSystemInternal(Type type, int order)
        {
            if (SortedObject<Type>.IndexOfElement(m_registerSystems, type) >= 0)
                throw new ApplicationException("Duplicated system registration detected.");
            
            m_registerSystems.Add(new SortedObject<Type>(order, type));
        }

        private void AddPluginInternal(Type type, int order)
        {
            if (SortedObject<Type>.IndexOfElement(m_registerPlugins, type) >= 0)
                throw new ApplicationException("Duplicated system registration detected.");
            
            m_registerPlugins.Add(new SortedObject<Type>(order, type));
        }

        public static WorldBuilder<TWorld> New() => new();
        
        public WorldBuilder<TWorld> InstallSystem<TSystem>(int order = 0) where TSystem : ISystem<TWorld>
        {
            AddSystemInternal(typeof(TSystem), order);
            return this;
        }
        
        public WorldBuilder<TWorld> InstallSystem(Type type, int order = 0)
        {
            if (!SystemType.IsAssignableFrom(type))
                throw new ApplicationException($"Type {type} is not a system!");
            
            AddSystemInternal(type, order);
            return this;
        }

        public WorldBuilder<TWorld> InstallPlugin<TPlugin>(int order = 0) where TPlugin : IPlugin<TWorld>
        {
            AddPluginInternal(typeof(TPlugin), order);
            return this;
        }
        
        public WorldBuilder<TWorld> InstallPlugin(Type type, int order = 0)
        {
            if (!PluginType.IsAssignableFrom(type))
                throw new ApplicationException($"Type {type} is not a plugin!");

            AddPluginInternal(type, order);
            return this;
        }

        public WorldBuilder<TWorld> SetEnvironmentData(object key, object value)
        {
            if (!m_envData.TryAdd(key, value))
            {
                m_envData[key] = value;
            }

            return this;
        }

        public TWorld Build()
        {
            var world = (TWorld) Activator.CreateInstance(typeof(TWorld), new object[] {this});
            
            m_registerSystems.Sort();
            m_registerPlugins.Sort();
            
            var dict = new ReadOnlyDictionary<Object, object>(m_envData);
            
            var systems = m_registerSystems
                .Select(static x => x.Element)
                .Select(Activator.CreateInstance)
                .Cast<ISystem<TWorld>>()
                .ToArray();
            
            var plugins = m_registerPlugins
                .Select(static x => x.Element)
                .Select(Activator.CreateInstance)
                .Cast<IPlugin<TWorld>>()
                .ToArray();

            PostCtorInjection.Invoke(world, new object[] {systems, plugins, dict});
            
            world.OnPreBuilt(systems, plugins, dict);
            for (var i = 0; i < plugins.Length; i++)
            {
                var pl = plugins[i];
                pl.OnBuilt(world, plugins, systems, dict);
            }
            world.OnPostBuilt(systems, plugins, dict);
            
            return world;
        }
    }

    /// <summary>
    /// States of world to help indicating system operating state.
    /// </summary>
    public enum WorldState
    {
        Created,
        Initializing,
        Run,
        Ticking,
        Deinitializing,
        Destroyed
    }
    
    /// <summary>
    /// Basic implementation of <see cref="IWorld{T}"/>
    /// </summary>
    /// <typeparam name="TWorld">The actual world underlying</typeparam>
    public abstract class World<TWorld> : IWorld<TWorld> where TWorld : World<TWorld>
    {

        #region Plugins

        private readonly IPlugin<TWorld>[] m_plugins;
        
        private readonly ComponentPlugin<TWorld> m_compPlugin;

        private readonly SystemPlugin<TWorld> m_sysPlugin;

        private readonly EntityPlugin<TWorld> m_entityPlugin;

        #endregion
        
        #region Basis
        
        private readonly IReadOnlyDictionary<object, object> m_envData;

        private WorldState m_state = WorldState.Created;

        
        #endregion
        
        #region Signals

        public Signal<WorldInitHandler<TWorld>> OnPreInit { get; } = new();

        public Signal<WorldInitHandler<TWorld>> OnInit { get; } = new();

        public Signal<WorldInitHandler<TWorld>> OnPostInit { get; } = new();

        public Signal<WorldTickHandler<TWorld>> OnCreateTick { get; } = new();

        public Signal<WorldTickHandler<TWorld>> OnBeforeTick { get; } = new();

        public Signal<WorldTickHandler<TWorld>> OnTick { get; } = new();

        public Signal<WorldTickHandler<TWorld>> OnAfterTick { get; } = new();

        public Signal<WorldTickHandler<TWorld>> OnDestroyTick { get; } = new();

        public Signal<WorldDeinitHandler<TWorld>> OnPreDeinit { get; } = new();

        public Signal<WorldDeinitHandler<TWorld>> OnDeinit { get; } = new();

        public Signal<WorldDeinitHandler<TWorld>> OnPostDeinit { get; } = new();

        #endregion
        
        #region APIs

        public WorldState State => m_state;
        
        public IEnumerable<ISystem<TWorld>> Systems 
            => m_sysPlugin.Systems;

        public IEnumerable<IEntity> Entities 
            => m_entityPlugin.Entities.Select(x => x.Value);

        /// <summary>
        /// Get entity graph of an entity by its id.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public IEntity GetEntityGraph(ulong entity)
        {
            return m_entityPlugin.Entities.GetValueOrDefault(entity);
        }
        
        public TSys GetSystem<TSys>() where TSys : ISystem<TWorld>
        {
            if (m_sysPlugin.SystemTransformer.TryGetValue(typeof(TSys), out var sysIdx))
            {
                return (TSys) m_sysPlugin.Systems[sysIdx];
            }

            return default;
        }

        public ulong GetEntityMask(ulong entity)
        {
            if (m_entityPlugin.Entities.TryGetValue(entity, out var g)) return g.Mask;
            return 0;
        }

        public ComponentRef[] GetComponents()
        {
            return m_compPlugin.GetAllComponentStores()
                .SelectMany(x => x.Cores)
                .Select(x => new ComponentRef(x))
                .ToArray();
        }

        public ComponentRef<TComp>[] GetComponents<TComp>() where TComp : struct, IComponent<TComp>
        {
            var store = m_compPlugin.GetComponentStore<TComp>(false);
            if (store == null) return Array.Empty<ComponentRef<TComp>>();

            return store.Cores.Select(x => new ComponentRef<TComp>(x)).ToArray();
        }

        public int GetComponents(ICollection<ComponentRef> result)
        {
            Assert.IsNotNull(result, "Collector is null!");
            
            var eum = m_compPlugin.GetAllComponentStores()
                .SelectMany(x => x.Cores)
                .Select(x => new ComponentRef(x));

            var initial = result.Count;
            foreach (var rf in eum) result.Add(rf);
            return result.Count - initial;
        }

        public int GetComponents<TComp>(ICollection<ComponentRef<TComp>> result) where TComp : struct, IComponent<TComp>
        {
            Assert.IsNotNull(result, "Collector is null!");
            
            var store = m_compPlugin.GetComponentStore<TComp>(false);
            if (store == null) return 0;

            var eum = store.Cores.Select(x => new ComponentRef<TComp>(x));
            var initial = result.Count;
            foreach (var rf in eum) result.Add(rf);
            return result.Count - initial;
        }

        public ComponentRef<TComp> AddComponent<TComp>(ulong entityId) where TComp : struct, IComponent<TComp>
        {
            Assert.IsTrue(m_entityPlugin.Entities.ContainsKey(entityId), "Entity is not created yet!");
            
            var core = m_compPlugin.CreateComponent<TComp>(entityId);
            return new ComponentRef<TComp>(core);
        }
        
        public ComponentRef<TComp> AddComponentOnNewEntity<TComp>(out ulong entityId, ulong mask) where TComp : struct, IComponent<TComp>
        {
            var entity = m_entityPlugin.RequestEntityGraph(mask);
            var core = m_compPlugin.CreateComponent<TComp>(entity.EntityId);
            entityId = entity.EntityId;
            return new ComponentRef<TComp>(core);
        }
        
        public ComponentRef AddComponent(Type type, ulong entityId)
        {
            Assert.IsTrue(m_entityPlugin.Entities.ContainsKey(entityId), "Entity is not created yet!");
            
            var core = m_compPlugin.CreateComponent(type, entityId);
            return new ComponentRef(core);
        }
        
        public ComponentRef AddComponentOnNewEntity(Type type, out ulong entityId, ulong mask)
        {
            var entity = m_entityPlugin.RequestEntityGraph(mask);
            var core = m_compPlugin.CreateComponent(type, entity.EntityId);
            entityId = entity.EntityId;
            return new ComponentRef(core);
        }

        public void DestroyComponent<TComp>(ComponentRef<TComp> comp) where TComp : struct, IComponent<TComp>
        {
            Assert.IsTrue(comp.NotNull, "Component Ref is not valid!");
            m_compPlugin.DestroyComponent<TComp>(comp.Core);
        }
        
        public void DestroyComponent(ComponentRef comp)
        {
            Assert.IsTrue(comp.NotNull, "Component Ref is not valid!");
            m_compPlugin.DestroyComponent(comp.Core);
        }

        public void DestroyEntity(ulong entityId)
        {
            var g = m_entityPlugin.GetEntityGraph(entityId);
            if (g == null) throw new ArgumentException("Entity is not allocated yet!");

            using (ListPool<ComponentRefCore>.Get(out var cos))
            {
                cos.AddRange(g.RwComponents);
                for (var i = 0; i < cos.Count; i++)
                {
                    m_compPlugin.DestroyComponent(cos[i]);
                }
            }
        }

        public TData GetEnvironmentData<TData>(object envKey, TData fallback = default)
        {
            
            if (m_envData.TryGetValue(envKey, out var d) && d is TData rd)
                return rd;
            
            return fallback;
        }

        public TPlugin GetPlugin<TPlugin>() where TPlugin : IPlugin<TWorld>
        {
            return m_plugins.OfType<TPlugin>().FirstOrDefault();
        }

        private void _postCtor(ISystem<TWorld>[] systems, IPlugin<TWorld>[] plugins, IReadOnlyDictionary<object, object> env)
        {
            Unsafe.AsRef(m_envData) = env;
            Unsafe.AsRef(m_plugins) = plugins;
            Unsafe.AsRef(m_sysPlugin) = plugins.OfType<SystemPlugin<TWorld>>().First();
            Unsafe.AsRef(m_compPlugin) = plugins.OfType<ComponentPlugin<TWorld>>().First();
            Unsafe.AsRef(m_entityPlugin) = plugins.OfType<EntityPlugin<TWorld>>().First();
        }

        private World() {}

        protected World(WorldBuilder<TWorld> builder) {}

        #endregion

        #region Executable
        
        public void Run()
        {
            if (m_state > WorldState.Created)
                throw new InvalidOperationException("This world is already started or terminated!");

            m_state = WorldState.Initializing;
            var w = (TWorld) this;
            var evt = new WorldEventInvoker<TWorld>(w);
            
            OnPreInit.Emit(in evt, WorldEventInvoker<TWorld>.InitEmitter);
            OnInit.Emit(in evt, WorldEventInvoker<TWorld>.InitEmitter);
            OnPostInit.Emit(in evt, WorldEventInvoker<TWorld>.InitEmitter);

            m_state = WorldState.Run;
        }

        public void Tick(float dt)
        {
            if (m_state != WorldState.Run)
                throw new InvalidOperationException("This world state is not at run!");
            
            m_state = WorldState.Ticking;
            var w = (TWorld) this;
            var evt = new WorldEventInvoker<TWorld>(w, dt);
            
            OnCreateTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);

            while (!m_sysPlugin.IsTickFinalized)
            {
                OnBeforeTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
                OnTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
                OnAfterTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
            }

            OnDestroyTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
            
            m_state = WorldState.Run;
        }

        public void Terminate()
        {
            if (m_state == WorldState.Ticking)
                throw new InvalidOperationException("Unable to terminate world when ticking!");
            
            if (m_state > WorldState.Deinitializing)
                throw new InvalidOperationException("This world is already terminated!");

            m_state = WorldState.Deinitializing;
            var w = (TWorld) this;
            var evt = new WorldEventInvoker<TWorld>(w);
            
            OnPreDeinit.Emit(in evt, WorldEventInvoker<TWorld>.DeinitEmitter);
            OnDeinit.Emit(in evt, WorldEventInvoker<TWorld>.DeinitEmitter);
            OnPostDeinit.Emit(in evt, WorldEventInvoker<TWorld>.DeinitEmitter);

            m_state = WorldState.Destroyed;
        }

        #endregion

        #region Implementation

        public virtual void OnPreBuilt(
            IReadOnlyList<ISystem<TWorld>> systems,
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyDictionary<object, object> envData) {}
        
        public virtual void OnPostBuilt(
            IReadOnlyList<ISystem<TWorld>> systems,
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyDictionary<object, object> envData) {}

        #endregion
    }
}