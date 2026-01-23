using System;
using System.Collections.Generic;
using TinyECS.Defines;
using TinyECS.Managers;
using TinyECS.Utils;

namespace TinyECS
{
    public class World : IWorld
    {

        #region Private Area

        private ManagerMediator m_mediator = null;

        private bool m_init = false;
        
        private bool m_ticking = false;
        
        private bool m_shutdown = false;

        #endregion

        #region EntityCache
        
        private SystemManager m_systemManager = null;

        #endregion
        
        
        public uint TickCount { get; private set; } = 0;

        public bool Ready => m_init && !m_shutdown;

        public bool Ticking => m_ticking;
        
        public TMgr GetManager<TMgr>() where TMgr : IWorldManager
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");
            
            if (m_mediator.Managers.TryGetValue(typeof(TMgr), out var manager))
                return (TMgr) manager;
            
            return default;
        }

        public void Startup()
        {
            Assertion.IsFalse(m_init, "World is already initialized");

            TickCount = 0;

            if (m_mediator == null)
            {
                using (ListPool<Type>.Get(out var reg))
                {
                    OnConstruct(reg);
                    if (!reg.Contains(typeof(SystemManager))) reg.Add(typeof(SystemManager));
                    
                    m_mediator = new ManagerMediator(this, reg);
                }

                m_systemManager = (SystemManager) m_mediator.Managers[typeof(SystemManager)];
            }
            
            m_mediator.Boot();
            
            m_init = true;
            m_ticking = false;
            m_shutdown = false;
            
            OnStart();
        }

        public void Shutdown()
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is already shutdown");
            Assertion.IsFalse(m_ticking, "World is ticking and should not be shutdown");
            
            OnShutdown();
            m_init = false;
            
            m_mediator.Shutdown();
        }

        public void BeginTick()
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");
            Assertion.IsFalse(m_ticking, "World is ticking and should not enter ticking again");
            
            m_ticking = true;
            if (TickCount > 0) TickCount++;
            OnTickBegin();
        }

        public void Tick(ulong tickMask = ulong.MaxValue)
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");
            Assertion.IsTrue(m_ticking, "World must enter ticking first.");
            
            OnTick(tickMask);
        }

        public void EndTick()
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");
            Assertion.IsTrue(m_ticking, "World must enter ticking first.");
            
            OnTickEnd();
            m_ticking = false;
            if (TickCount == 0) TickCount++;
        }

        #region Lifecircle Events

        protected virtual void OnConstruct(IList<Type> reg)
        {
            reg.Add(typeof(ComponentManager));
            reg.Add(typeof(EntityManager));
            reg.Add(typeof(EntityMatchManager));
        }

        protected virtual void OnStart()
        {}

        protected virtual void OnTickBegin()
        {
            m_systemManager.TeardownSystems();
        }

        protected virtual void OnTick(ulong tickMask)
        {
            m_systemManager.ExecuteSystems(tickMask);
        }

        protected virtual void OnTickEnd()
        {
            m_systemManager.CleanupSystems();
        }

        protected virtual void OnShutdown()
        {}

        #endregion
    }
}