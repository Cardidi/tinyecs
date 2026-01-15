using TinyECS.Defines;
using TinyECS.Utils;

namespace TinyECS.Managers
{
    public delegate void SystemBeginExecute(IWorld world, ISystem system);
    
    public delegate void SystemEndExecute(IWorld world, ISystem system);
    
    /// <summary>
    /// Plugin on manage system schedule and tick.
    /// </summary>
    public sealed class SystemManager : IWorldManager
    {

        public IWorld World { get; private set; }
        
        /// <summary>
        /// All registered systems in manager.
        /// </summary>
        public IReadOnlyList<ISystem> Systems => m_systems;

        /// <summary>
        /// All registered systems in manager by type.
        /// </summary>
        public IReadOnlyDictionary<Type, ISystem> SystemTransformer => m_systemTransformer;
        
        public Signal<SystemBeginExecute> OnSystemBeginExecute { get; } = new();
        
        public Signal<SystemEndExecute> OnSystemEndExecute { get; } = new();

        private readonly List<ISystem> m_systems = new();

        private readonly Dictionary<Type, ISystem> m_systemTransformer = new();
        
        private readonly Queue<Type> m_delSystems = new();
        
        private readonly Queue<Type> m_addSystems = new();

        private bool m_firstInit = false;

        private bool m_shutdown = false;

        private void SystemPoll(ISystem system, ulong systemMask)
        {
            var selected = (system.TickGroup & systemMask) > 0;
            if (selected)
            {
                OnSystemBeginExecute.Emit(World, system, static (h, w, s) => h(w, s));
                try
                {
                    system.OnTick(World);
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }
                OnSystemEndExecute.Emit(World, system, static (h, w, s) => h(w, s));
            }
        }

        private void CreateSystem(ISystem system)
        {
            try
            {
                system.OnCreate(World);
            }
            catch (Exception e)
            {
                Log.Exp(e);
            }
        }

        private void DestroySystem(ISystem system)
        {
            try
            {
                system.OnDestroy(World);
            }
            catch (Exception e)
            {
                Log.Exp(e);
            }
        } 

        private ISystem InstantSystem(Type systemType)
        {
            Assertion.IsParentTypeTo<ISystem>(systemType);
            Assertion.IsFalse(m_systemTransformer.ContainsKey(systemType));
            
            return (ISystem) Activator.CreateInstance(systemType);
        }
        
        public void ExecuteSystem(ulong systemMask)
        {
            Assertion.IsTrue(m_firstInit);
            Assertion.IsFalse(m_shutdown);
            
            for (var i = 0; i < Systems.Count; i++)
            {
                var system = Systems[i];
                if (m_delSystems.Contains(system.GetType())) continue;
                SystemPoll(system, systemMask);
            }
            
            while (m_delSystems.TryDequeue(out var type))
            {
                var sys = m_systemTransformer[type];
                m_systemTransformer.Remove(type);
                m_systems.Remove(sys);
                DestroySystem(sys);
            }
        }
        
        public void RegisterSystem(Type systemType)
        {
            Assertion.IsFalse(m_shutdown);
            Assertion.IsNotNull(systemType);
            
            if (m_firstInit)
            {
                var sys = InstantSystem(systemType);
                m_systemTransformer.Add(systemType, sys);
                m_systems.Add(sys);
                CreateSystem(sys);
            }
            else
            {
                if (!m_addSystems.Contains(systemType)) m_addSystems.Enqueue(systemType);
            }
        }

        public void UnregisterSystem(Type systemType)
        {
            Assertion.IsFalse(m_shutdown);
            Assertion.IsNotNull(systemType);
            Assertion.IsTrue(m_systemTransformer.ContainsKey(systemType) && !m_delSystems.Contains(systemType));
            var sys = m_systemTransformer[systemType];

            if (m_firstInit)
            {
                m_delSystems.Enqueue(systemType);
            }
            else
            {
                m_systemTransformer.Remove(systemType);
                m_systems.Remove(sys);
                DestroySystem(sys);
            }
        }
        
        
        public void OnManagerCreated(IWorld world)
        {
            m_firstInit = false;
            m_shutdown = false;
        }

        public void OnWorldStarted(IWorld world)
        {
            m_firstInit = true;
            if (m_addSystems.TryDequeue(out var type))
            {
                var sys = InstantSystem(type);
                m_systemTransformer.Add(type, sys);
                m_systems.Add(sys);
                CreateSystem(sys);
            }
        }

        public void OnWorldEnded(IWorld world)
        {
            m_shutdown = true;
            
            m_delSystems.Clear();
            foreach (var system in Systems) m_delSystems.Enqueue(system.GetType());
            
            while (m_delSystems.TryDequeue(out var type))
            {
                var sys = m_systemTransformer[type];
                m_systemTransformer.Remove(type);
                m_systems.Remove(sys);
                DestroySystem(sys);
            }
        }

        public void OnManagerDestroyed(IWorld world)
        {
        }
    }
}