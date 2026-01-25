using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TinyECS.Defines;
using TinyECS.Utils;

namespace TinyECS.Managers
{
    public delegate void SystemTeardown(IWorld world);
    
    public delegate void SystemBeginExecute(IWorld world, ISystem system);
    
    public delegate void SystemEndExecute(IWorld world, ISystem system);

    public delegate void SystemCleanup(IWorld world);
    
    /// <summary>
    /// Plugin on manage system schedule and tick.
    /// </summary>
    public sealed class SystemManager : IWorldManager
    {

        public IWorld World { get; }
        
        /// <summary>
        /// All registered systems in manager.
        /// </summary>
        public IReadOnlyList<ISystem> Systems => m_systems;

        /// <summary>
        /// All registered systems in manager by type.
        /// </summary>
        public IReadOnlyDictionary<Type, ISystem> SystemTransformer => m_systemTransformer;
        
        public Signal<SystemTeardown> OnSystemTeardown { get; } = new();
        
        public Signal<SystemBeginExecute> OnSystemBeginExecute { get; } = new();
        
        public Signal<SystemEndExecute> OnSystemEndExecute { get; } = new();
        
        public Signal<SystemCleanup> OnSystemCleanup { get; } = new();

        private readonly List<ISystem> m_systems = new();

        private readonly Dictionary<Type, ISystem> m_systemTransformer = new();
        
        private readonly Queue<Type> m_delSystems = new();
        
        private readonly Queue<Type> m_addSystems = new();

        private readonly Injector m_injector;

        private bool m_init = false;

        private bool m_shutdown = false;

        private bool m_changable = true;

        private void _systemPoll(ISystem system, ulong systemMask)
        {
            var selected = (system.TickGroup & systemMask) > 0;
            if (selected)
            {
                OnSystemBeginExecute.Emit(World, system, static (h, w, s) => h(w, s));
                try
                {
                    system.OnTick();
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }
                OnSystemEndExecute.Emit(World, system, static (h, w, s) => h(w, s));
            }
        }

        private void _createSystem(ISystem system)
        {
            try
            {
                system.OnCreate();
            }
            catch (Exception e)
            {
                Log.Exp(e);
            }
        }

        private void _destroySystem(ISystem system)
        {
            try
            {
                system.OnDestroy();
            }
            catch (Exception e)
            {
                Log.Exp(e);
            }
        } 

        private ISystem _instantSystem(Type systemType)
        {
            Assertion.IsParentTypeTo<ISystem>(systemType);
            Assertion.IsFalse(m_systemTransformer.ContainsKey(systemType));
            
            var sys = (ISystem) FormatterServices.GetUninitializedObject(systemType);
            if (m_injector != null) m_injector.InjectConstructor(sys);
            return sys;
        }

        public void TeardownSystems()
        {
            Assertion.IsTrue(m_init, "SystemManager is not initialized yet.");
            Assertion.IsFalse(m_shutdown, "SystemManager has already shutdown.");
            
            m_changable = false;

            for (var i = m_addSystems.Count; i > 0; i--)
            {
                var systemType = m_addSystems.Dequeue();
                var sys = _instantSystem(systemType);
                m_systemTransformer.Add(systemType, sys);
                m_systems.Add(sys);
                _createSystem(sys);
            }
            
            OnSystemTeardown.Emit(World, static (h, w) => h(w));
        }
        
        public void ExecuteSystems(ulong systemMask)
        {
            Assertion.IsTrue(m_init, "SystemManager is not initialized yet.");
            Assertion.IsFalse(m_shutdown, "SystemManager has already shutdown.");
            
            for (var i = 0; i < Systems.Count; i++)
            {
                var system = Systems[i];
                _systemPoll(system, systemMask);
            }
        }

        public void CleanupSystems()
        {
            Assertion.IsTrue(m_init, "SystemManager is not initialized yet.");
            Assertion.IsFalse(m_shutdown, "SystemManager has already shutdown.");
            
            while (m_delSystems.TryDequeue(out var type))
            {
                var sys = m_systemTransformer[type];
                m_systemTransformer.Remove(type);
                m_systems.Remove(sys);
                _destroySystem(sys);
            }
            
            m_changable = true;
            
            OnSystemCleanup.Emit(World, static (h, w) => h(w));
        }
        
        public void RegisterSystem(Type systemType)
        {
            Assertion.IsFalse(m_shutdown, "SystemManager has already shutdown.");
            Assertion.IsNotNull(systemType);
            Assertion.IsParentTypeTo<ISystem>(systemType);

            if (m_changable)
            {
                var sys = _instantSystem(systemType);
                m_systemTransformer.Add(systemType, sys);
                m_systems.Add(sys);
                _createSystem(sys);
            }
            else
            {
                if (!m_systemTransformer.ContainsKey(systemType) && !m_addSystems.Contains(systemType))
                {
                    m_addSystems.Enqueue(systemType);
                }
            }
        }

        public void UnregisterSystem(Type systemType)
        {
            Assertion.IsFalse(m_shutdown, "SystemManager has already shutdown.");
            Assertion.IsNotNull(systemType);
            Assertion.IsParentTypeTo<ISystem>(systemType);
            
            var sys = m_systemTransformer[systemType];

            if (m_changable)
            {
                m_systemTransformer.Remove(systemType);
                m_systems.Remove(sys);
                _destroySystem(sys);
            }
            else
            {
                if (m_systemTransformer.ContainsKey(systemType) && !m_delSystems.Contains(systemType))
                    m_delSystems.Enqueue(systemType);
            }
        }


        #region EventHandlers

        public void OnManagerCreated()
        {
            m_init = false;
            m_shutdown = false;
        }

        public void OnWorldStarted()
        {
            m_init = true;
            if (m_addSystems.TryDequeue(out var type))
            {
                var sys = _instantSystem(type);
                m_systemTransformer.Add(type, sys);
                m_systems.Add(sys);
                _createSystem(sys);
            }
        }

        public void OnWorldEnded()
        {
            m_shutdown = true;
            
            m_addSystems.Clear();
            m_delSystems.Clear();
            foreach (var system in m_systems) m_delSystems.Enqueue(system.GetType());
            
            while (m_delSystems.TryDequeue(out var type))
            {
                var sys = m_systemTransformer[type];
                m_systemTransformer.Remove(type);
                m_systems.Remove(sys);
                _destroySystem(sys);
            }
        }

        public void OnManagerDestroyed()
        {
        }

        #endregion

        public SystemManager(IWorld world, Injector injector)
        {
            World = world;
            m_injector = injector;
        }
    }
}