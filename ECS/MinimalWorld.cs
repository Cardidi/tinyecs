using System;
using System.Collections.Generic;
using TinyECS.Defines;
using TinyECS.Managers;
using TinyECS.Utils;

namespace TinyECS
{
    /// <summary>
    /// A minimal implementation of IWorld. It provides a basic world structure with essential managers.
    /// You can extend this class to create your own world with custom managers.
    /// </summary>
    public abstract class MinimalWorld : IWorld
    {
        #region Private Area

        private ManagerMediator m_mediator = null;
        private bool m_init = false;
        private bool m_ticking = false;
        private bool m_shutdown = false;

        #endregion

        /// <summary>
        /// Get the current tick count of the world.
        /// </summary>
        public uint TickCount { get; private set; } = 0;
        
        /// <summary>
        /// Get the injection container of this world
        /// </summary>
        public Injector Injection { get; } = new Injector();
        
        /// <summary>
        /// Get the manager mediator for managing managers.
        /// </summary>
        public ManagerMediator Mediator => m_mediator;
        public bool Ready => m_init && !m_shutdown;
        
        /// <summary>
        /// Get the tick state of the world.
        /// </summary>
        public bool Ticking => m_ticking;

        /// <summary>
        /// Get a manager by its type.
        /// </summary>
        public TMgr GetManager<TMgr>() where TMgr : IWorldManager
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");

            if (m_mediator.Managers.TryGetValue(typeof(TMgr), out var manager))
                return (TMgr)manager;

            throw new InvalidOperationException($"Manager of type {typeof(TMgr).Name} not found.");
        }

        /// <summary>
        /// Startup the world.
        /// </summary>
        public void Startup()
        {
            Assertion.IsFalse(m_init, "World is already initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");

            // Initialize state
            TickCount = 0;
            m_ticking = false;

            // Create mediator if not exists
            if (m_mediator == null)
            {
                var managerTypes = new List<Type>();
                try
                {
                    OnRegisterManager(managerTypes);
                }
                catch (Exception e)
                {
                    Log.Exp(e, nameof(OnRegisterManager));
                }
                
                // Add core managers after user registration
                if (!managerTypes.Contains(typeof(ComponentManager)))
                    managerTypes.Add(typeof(ComponentManager));
                
                if (!managerTypes.Contains(typeof(EntityManager)))
                    managerTypes.Add(typeof(EntityManager));
                
                if (!managerTypes.Contains(typeof(EntityMatchManager)))
                    managerTypes.Add(typeof(EntityMatchManager));
                
                if (!managerTypes.Contains(typeof(SystemManager)))
                    managerTypes.Add(typeof(SystemManager));
                
                // Create mediator and initialize managers
                m_mediator = new ManagerMediator(this, Injection, managerTypes);
                
                // Register dependencies for injection
                Injection.Register(Injection);
                Injection.Register(this);
                foreach (var manager in m_mediator.Managers.Values)
                    Injection.Register(manager);
                
                // Construct managers using dependency injection
                m_mediator.Construct();
                try
                {
                    OnConstruct();
                }
                catch (Exception e)
                {
                    Log.Exp(e, nameof(OnConstruct));
                }
            }

            // Boot the mediator
            m_mediator.Boot();

            // Finalize initialization
            m_init = true;
            try
            {
                OnStart();
            }
            catch (Exception e)
            {
                Log.Exp(e, nameof(OnStart));
            }
        }

        /// <summary>
        /// Shutdown the world.
        /// </summary>
        public void Shutdown()
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is already shutdown");
            Assertion.IsFalse(m_ticking, "World is ticking and should not be shutdown");

            // Call user-defined shutdown logic
            try
            {
                OnShutdown();
            }
            catch (Exception e)
            {
                Log.Exp(e, nameof(OnShutdown));
            }

            // Shutdown mediator
            m_mediator.Shutdown();

            // Update state
            m_init = false;
            m_shutdown = true;
        }

        /// <summary>
        /// Begin a new tick.
        /// </summary>
        public void BeginTick()
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");
            Assertion.IsFalse(m_ticking, "World is already ticking");

            // Increment tick count at the beginning of each tick
            TickCount++;
            m_ticking = true;
            try
            {
                OnTickBegin();
            }
            catch (Exception e)
            {
                Log.Exp(e, nameof(OnTickBegin));
            }
        }

        /// <summary>
        /// Execute the tick.
        /// </summary>
        public void Tick(ulong tickMask = ulong.MaxValue)
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");
            Assertion.IsTrue(m_ticking, "World must enter ticking first");

            try
            {
                OnTick(tickMask);
            }
            catch (Exception e)
            {
                Log.Exp(e, nameof(OnTick));
            }
        }

        /// <summary>
        /// End the current tick.
        /// </summary>
        public void EndTick()
        {
            Assertion.IsTrue(m_init, "World is not initialized");
            Assertion.IsFalse(m_shutdown, "World is shutdown");
            Assertion.IsTrue(m_ticking, "World must be in ticking state");

            try
            {
                OnTickEnd();
            }
            catch (Exception e)
            {
                Log.Exp(e, nameof(OnTickEnd));
            }
            m_ticking = false;
        }

        #region Lifecycle Events

        /// <summary>
        /// Register managers for the world.
        /// </summary>
        protected abstract void OnRegisterManager(IList<Type> reg);

        protected abstract void OnConstruct();
        protected abstract void OnStart();
        protected abstract void OnTickBegin();
        protected abstract void OnTick(ulong tickMask);
        protected abstract void OnTickEnd();
        protected abstract void OnShutdown();

        #endregion
    }
}