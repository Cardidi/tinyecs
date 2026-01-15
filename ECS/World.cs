// // ReSharper disable ForCanBeConvertedToForeach
//
// using System.Collections.ObjectModel;
// using System.Reflection;
// using TinyECS.Defines;
// using TinyECS.Managers;
// using TinyECS.Utils;
// using Object = System.Object;
//
// namespace TinyECS {
//
//     /// <summary>
//     /// World builder. It can be cached and await to create any new world.
//     /// </summary>
//     /// <typeparam name="TWorld">Created world type</typeparam>
//     public sealed class WorldBuilder
//     {
//         private static readonly Type SystemTypes = typeof(ISystem);
//         
//         private static readonly Type ManagerTypes = typeof(IWorldManager);
//
//         private static readonly MethodInfo PostCtorInjection =
//             typeof(World).GetMethod("_postCtor", BindingFlags.Instance |  BindingFlags.NonPublic);
//         
//         private readonly List<SortedObject<Type>> m_registerSystems = new();
//
//         private readonly List<SortedObject<Type>> m_registerManagers = new();
//         
//         private WorldBuilder() {}
//
//         private void AddSystemInternal(Type type, int order)
//         {
//             if (SortedObject<Type>.IndexOfElement(m_registerSystems, type) >= 0)
//                 throw new ApplicationException("Duplicated system registration detected.");
//             
//             m_registerSystems.Add(new SortedObject<Type>(order, type));
//         }
//
//         private void AddManagerInternal(Type type, int order)
//         {
//             if (SortedObject<Type>.IndexOfElement(m_registerManagers, type) >= 0)
//                 throw new ApplicationException("Duplicated system registration detected.");
//             
//             m_registerManagers.Add(new SortedObject<Type>(order, type));
//         }
//
//         public static WorldBuilder New() => new();
//         
//         public WorldBuilder InstallSystem<TSystem>(int order = 0) where TSystem : ISystem
//         {
//             AddSystemInternal(typeof(TSystem), order);
//             return this;
//         }
//         
//         public WorldBuilder InstallSystem(Type type, int order = 0)
//         {
//             if (!SystemTypes.IsAssignableFrom(type))
//                 throw new ApplicationException($"Type {type} is not a system!");
//             
//             AddSystemInternal(type, order);
//             return this;
//         }
//
//         public WorldBuilder InstallManager<TMgr>(int order = 0) where TMgr : IWorldManager
//         {
//             AddManagerInternal(typeof(TMgr), order);
//             return this;
//         }
//         
//         public WorldBuilder InstallManager(Type type, int order = 0)
//         {
//             if (!ManagerTypes.IsAssignableFrom(type))
//                 throw new ApplicationException($"Type {type} is not a plugin!");
//
//             AddManagerInternal(type, order);
//             return this;
//         }
//
//         public TWorld Build<TWorld>() where TWorld : World
//         {
//             var world = (TWorld) Activator.CreateInstance(typeof(TWorld), new object[] {this});
//             
//             m_registerSystems.Sort();
//             m_registerManagers.Sort();
//             
//             var systems = m_registerSystems
//                 .Select(static x => x.Element)
//                 .Select(Activator.CreateInstance)
//                 .Cast<ISystem<TWorld>>()
//                 .ToArray();
//             
//             var plugins = m_registerManagers
//                 .Select(static x => x.Element)
//                 .Select(Activator.CreateInstance)
//                 .Cast<IPlugin<TWorld>>()
//                 .ToArray();
//
//             PostCtorInjection.Invoke(world, new object[] {systems, plugins, dict});
//             
//             world.OnPreBuilt(systems, plugins, dict);
//             for (var i = 0; i < plugins.Length; i++)
//             {
//                 var pl = plugins[i];
//                 pl.OnConstruct(world, plugins, systems, dict);
//             }
//             world.OnPostBuilt(systems, plugins, dict);
//             
//             return world;
//         }
//     }
//
//     /// <summary>
//     /// States of world to help indicating system operating state.
//     /// </summary>
//     public enum WorldState
//     {
//         Created,
//         Initializing,
//         Run,
//         Ticking,
//         Deinitializing,
//         Destroyed
//     }
//     
//     /// <summary>
//     /// Basic implementation of <see cref="IWorld{T}"/>
//     /// </summary>
//     /// <typeparam name="TWorld">The actual world underlying</typeparam>
//     public abstract class World : IWorld
//     {
//         private ManagerMediator m_mediator;
//
//         #region CachedManager
//         
//         private ComponentManager m_compManager;
//
//         private SystemManager m_sysManager;
//
//         private EntityManager m_entityManager;
//
//         #endregion
//         
//         #region Basis
//
//         private WorldState m_state = WorldState.Created;
//
//         
//         #endregion
//         
//         #region APIs
//
//         public WorldState State => m_state;
//         
//         public IEnumerable<ISystem<IWorld>> Systems 
//             => m_sysManager.Systems;
//
//         public IEnumerable<IEntity> Entities 
//             => m_entityManager.Entities.Select(x => x.Value);
//
//         /// <summary>
//         /// Get entity graph of an entity by its id.
//         /// </summary>
//         /// <param name="entity"></param>
//         /// <returns></returns>
//         public IEntity GetEntityGraph(ulong entity)
//         {
//             return m_entityManager.Entities.GetValueOrDefault(entity);
//         }
//         
//         public TSys GetSystem<TSys>() where TSys : ISystem
//         {
//             if (m_sysManager.SystemTransformer.TryGetValue(typeof(TSys), out var sysIdx))
//             {
//                 return (TSys) m_sysManager.Systems[sysIdx];
//             }
//
//             return default;
//         }
//
//         public ulong GetEntityMask(ulong entity)
//         {
//             if (m_entityManager.Entities.TryGetValue(entity, out var g)) return g.Mask;
//             return 0;
//         }
//
//         public ComponentRef[] GetComponents()
//         {
//             return m_compManager.GetAllComponentStores()
//                 .SelectMany(x => x.Cores)
//                 .Select(x => new ComponentRef(x))
//                 .ToArray();
//         }
//
//         public ComponentRef<TComp>[] GetComponents<TComp>() where TComp : struct, IComponent<TComp>
//         {
//             var store = m_compManager.GetComponentStore<TComp>(false);
//             if (store == null) return Array.Empty<ComponentRef<TComp>>();
//
//             return store.Cores.Select(x => new ComponentRef<TComp>(x)).ToArray();
//         }
//
//         public int GetComponents(ICollection<ComponentRef> result)
//         {
//             Assertion.IsNotNull(result, "Collector is null!");
//             
//             var eum = m_compManager.GetAllComponentStores()
//                 .SelectMany(x => x.Cores)
//                 .Select(x => new ComponentRef(x));
//
//             var initial = result.Count;
//             foreach (var rf in eum) result.Add(rf);
//             return result.Count - initial;
//         }
//
//         public int GetComponents<TComp>(ICollection<ComponentRef<TComp>> result) where TComp : struct, IComponent<TComp>
//         {
//             Assertion.IsNotNull(result, "Collector is null!");
//             
//             var store = m_compManager.GetComponentStore<TComp>(false);
//             if (store == null) return 0;
//
//             var eum = store.Cores.Select(x => new ComponentRef<TComp>(x));
//             var initial = result.Count;
//             foreach (var rf in eum) result.Add(rf);
//             return result.Count - initial;
//         }
//
//         public TMgr GetManager<TMgr>() where TMgr : IWorldManager
//         {
//             if (m_mediator.Managers.TryGetValue(typeof(TMgr), out var mgr))
//             {
//                 return (TMgr) mgr;
//             }
//
//             return default;
//         }
//
//         public ComponentRef<TComp> AddComponent<TComp>(ulong entityId) where TComp : struct, IComponent<TComp>
//         {
//             Assertion.IsTrue(m_entityManager.Entities.ContainsKey(entityId), "Entity is not created yet!");
//             
//             var core = m_compManager.CreateComponent<TComp>(entityId);
//             return new ComponentRef<TComp>(core);
//         }
//         
//         public ComponentRef<TComp> AddComponentOnNewEntity<TComp>(out ulong entityId, ulong mask) where TComp : struct, IComponent<TComp>
//         {
//             var entity = m_entityManager.RequestEntityGraph(mask);
//             var core = m_compManager.CreateComponent<TComp>(entity.EntityId);
//             entityId = entity.EntityId;
//             return new ComponentRef<TComp>(core);
//         }
//         
//         public ComponentRef AddComponent(Type type, ulong entityId)
//         {
//             Assertion.IsTrue(m_entityManager.Entities.ContainsKey(entityId), "Entity is not created yet!");
//             
//             var core = m_compManager.CreateComponent(type, entityId);
//             return new ComponentRef(core);
//         }
//         
//         public ComponentRef AddComponentOnNewEntity(Type type, out ulong entityId, ulong mask)
//         {
//             var entity = m_entityManager.RequestEntityGraph(mask);
//             var core = m_compManager.CreateComponent(type, entity.EntityId);
//             entityId = entity.EntityId;
//             return new ComponentRef(core);
//         }
//
//         public void DestroyComponent<TComp>(ComponentRef<TComp> comp) where TComp : struct, IComponent<TComp>
//         {
//             Assertion.IsTrue(comp.NotNull, "Component Ref is not valid!");
//             m_compManager.DestroyComponent<TComp>(comp.Core);
//         }
//         
//         public void DestroyComponent(ComponentRef comp)
//         {
//             Assertion.IsTrue(comp.NotNull, "Component Ref is not valid!");
//             m_compManager.DestroyComponent(comp.Core);
//         }
//
//         public void DestroyEntity(ulong entityId)
//         {
//             var g = m_entityManager.GetEntityGraph(entityId);
//             if (g == null) throw new ArgumentException("Entity is not allocated yet!");
//
//             using (ListPool<ComponentRefCore>.Get(out var cos))
//             {
//                 cos.AddRange(g.RwComponents);
//                 for (var i = 0; i < cos.Count; i++)
//                 {
//                     m_compManager.DestroyComponent(cos[i]);
//                 }
//             }
//         }
//
//         private void _postCtor(ISystem<TWorld>[] systems, IPlugin<TWorld>[] plugins, IReadOnlyDictionary<object, object> env)
//         {
//             m_envData = env;
//             m_plugins = plugins;
//             m_sysManager = plugins.OfType<SystemManager<TWorld>>().First();
//             m_compManager = plugins.OfType<ComponentManager>().First();
//             m_entityManager = plugins.OfType<EntityManager>().First();
//         }
//
//         private World() {}
//
//         protected World(WorldBuilder builder) {}
//
//         #endregion
//
//         #region Executable
//         
//         public void Run()
//         {
//             if (m_state > WorldState.Created)
//                 throw new InvalidOperationException("This world is already started or terminated!");
//
//             m_state = WorldState.Initializing;
//             var w = (TWorld) this;
//             var evt = new WorldEventInvoker<TWorld>(w);
//             
//             OnPreInit.Emit(in evt, WorldEventInvoker<TWorld>.InitEmitter);
//             OnInit.Emit(in evt, WorldEventInvoker<TWorld>.InitEmitter);
//             OnPostInit.Emit(in evt, WorldEventInvoker<TWorld>.InitEmitter);
//
//             m_state = WorldState.Run;
//         }
//
//         public void Tick(float dt)
//         {
//             if (m_state != WorldState.Run)
//                 throw new InvalidOperationException("This world state is not at run!");
//             
//             m_state = WorldState.Ticking;
//             var w = (TWorld) this;
//             var evt = new WorldEventInvoker<TWorld>(w, dt);
//             
//             OnCreateTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
//
//             while (!m_sysManager.IsTickFinalized)
//             {
//                 OnBeforeTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
//                 OnTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
//                 OnAfterTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
//             }
//
//             OnDestroyTick.Emit(in evt, WorldEventInvoker<TWorld>.TickEmitter);
//             
//             m_state = WorldState.Run;
//         }
//
//         public void Terminate()
//         {
//             if (m_state == WorldState.Ticking)
//                 throw new InvalidOperationException("Unable to terminate world when ticking!");
//             
//             if (m_state > WorldState.Deinitializing)
//                 throw new InvalidOperationException("This world is already terminated!");
//
//             m_state = WorldState.Deinitializing;
//             var w = (TWorld) this;
//             var evt = new WorldEventInvoker<TWorld>(w);
//             
//             OnPreDeinit.Emit(in evt, WorldEventInvoker<TWorld>.DeinitEmitter);
//             OnDeinit.Emit(in evt, WorldEventInvoker<TWorld>.DeinitEmitter);
//             OnPostDeinit.Emit(in evt, WorldEventInvoker<TWorld>.DeinitEmitter);
//
//             m_state = WorldState.Destroyed;
//         }
//
//         #endregion
//
//         #region Implementation
//
//         public virtual void OnPreBuilt(
//             IReadOnlyList<ISystem<TWorld>> systems,
//             IReadOnlyList<IPlugin<TWorld>> plugins,
//             IReadOnlyDictionary<object, object> envData) {}
//         
//         public virtual void OnPostBuilt(
//             IReadOnlyList<ISystem<TWorld>> systems,
//             IReadOnlyList<IPlugin<TWorld>> plugins,
//             IReadOnlyDictionary<object, object> envData) {}
//
//         #endregion
//     }
// }