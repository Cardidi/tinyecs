using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyECS.Defines;
using TinyECS.Utils;

namespace TinyECS.Managers
{
    public interface IManagerRegister
    {
        /// <summary>
        /// Register a manager implementation with its interface type.
        /// TImp is the interface type and TMgr is the implementation that inherits from TImp.
        /// </summary>
        /// <typeparam name="TImp">The interface type of the manager</typeparam>
        /// <typeparam name="TMgr">The implementation type of the manager, which should inherit from TImp</typeparam>
        public void RegisterManager<TImp, TMgr>() where TMgr : TImp where TImp : IWorldManager;
        
        /// <summary>
        /// Register a manager directly by its concrete type.
        /// The same type is used as both interface and implementation.
        /// </summary>
        /// <typeparam name="T">The concrete manager type that implements IWorldManager</typeparam>
        public void RegisterManager<T>() where T : IWorldManager;
        
        /// <summary>
        /// Check if a manager type (either interface or implementation) is already registered.
        /// </summary>
        /// <typeparam name="T">The type to check for registration</typeparam>
        /// <returns>True if the type is already registered, false otherwise</returns>
        public bool AlreadyRegistered<T>();
    }
    
    /// <summary>
    /// Mediator to manage world managers.
    /// </summary>
    public sealed class ManagerMediator : IManagerRegister
    {
        private static readonly Type ParentType = typeof(IWorldManager);
        
        private ImmutableDictionary<Type, IWorldManager> m_managerMap;
        private readonly List<(Type interfaceType, Type implementationType)> m_registeredManagers;
        private readonly IWorld m_world;
        private readonly Injector m_injector;
        private bool m_constructed = false;

        public ManagerMediator(IWorld world, Injector injector)
        {
            Assertion.IsNotNull(world);

            m_world = world;
            m_injector = injector;
            m_registeredManagers = new List<(Type, Type)>();
            
            // Initialize with empty manager map initially
            m_managerMap = ImmutableDictionary<Type, IWorldManager>.Empty;
        }
        
        public bool Booted { get; private set; }
        
        public IReadOnlyDictionary<Type, IWorldManager> Managers => m_managerMap;

        public void RegisterManager<TImp, TMgr>() where TMgr : TImp where TImp : IWorldManager
        {
            Assertion.IsFalse(m_constructed, "Cannot register manager after construction");
            
            // Check if either type is already registered
            foreach (var (interfaceType, implementationType) in m_registeredManagers)
            {
                if (interfaceType == typeof(TImp) || implementationType == typeof(TMgr))
                {
                    Log.Err($"Manager type already registered: Interface={interfaceType}, Implementation={implementationType}");
                    return;
                }
            }
            
            m_registeredManagers.Add((typeof(TImp), typeof(TMgr)));
        }

        public void RegisterManager<T>() where T : IWorldManager
        {
            Assertion.IsFalse(m_constructed, "Cannot register manager after construction");
            
            var type = typeof(T);
            
            // Check if the type is already registered
            foreach (var (interfaceType, implementationType) in m_registeredManagers)
            {
                if (interfaceType == type || implementationType == type)
                {
                    Log.Err($"Manager type already registered: {type}");
                    return;
                }
            }
            
            // Register the same type as both interface and implementation
            m_registeredManagers.Add((type, type));
        }

        public bool AlreadyRegistered<T>()
        {
            var type = typeof(T);
            
            // Check in registered list (before construction)
            foreach (var (interfaceType, implementationType) in m_registeredManagers)
            {
                if (interfaceType == type || implementationType == type)
                {
                    return true;
                }
            }

            return false;
        }

        public void Construct()
        {
            if (m_constructed) return; // Prevent double construction
            
            using (ListPool<IWorldManager>.Get(out var generated))
            {
                var built = ImmutableDictionary.CreateBuilder<Type, IWorldManager>();

                foreach (var (interfaceType, implementationType) in m_registeredManagers)
                {
                    if (implementationType == null)
                    {
                        Log.Err($"Null registered manager type found!");
                        continue;
                    }

                    if (!ParentType.IsAssignableFrom(implementationType))
                    {
                        Log.Err($"Not a valid manager type {implementationType}");
                        continue;
                    }

                    if (built.ContainsKey(implementationType) &&
                        (implementationType == interfaceType || built.ContainsKey(interfaceType)))
                    {
                        Log.Err($"Duplicated manager type {interfaceType} or {implementationType}");
                        continue;
                    }

                    try
                    {
                        var manager = (IWorldManager) Activator.CreateInstance(implementationType, m_world);
                        
                        // Register manager into injector if possible
                        if (m_injector != null) m_injector.Register(manager);

                        // Register both the interface type and implementation type to the same manager instance
                        if (interfaceType != implementationType) built.Add(interfaceType, manager);
                        built.Add(implementationType, manager);
                        generated.Add(manager);
                    }
                    catch (Exception e)
                    {
                        Log.Err($"Failed to create manager instance for {implementationType}: {e.Message}");
                        continue;
                    }
                }

                m_managerMap = built.ToImmutable();

                // Now inject dependencies for all managers
                if (m_injector != null)
                {
                    foreach (var mgr in generated) m_injector.InjectConstructor(mgr);
                }
            }
            
            m_constructed = true;
        }

        public void Boot()
        {
            if (Booted) return;
            Booted = true;
            
            var managersToUse = m_managerMap;
            
            foreach (var mgr in managersToUse.Values)
            {
                try
                {
                    mgr.OnManagerCreated();
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }
            }
            
            foreach (var mgr in managersToUse.Values)
            {
                try
                {
                    mgr.OnWorldStarted();
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }
            }
        }

        public void Shutdown()
        {
            if (!Booted) return;
            Booted = false;
            
            var managersToUse = m_managerMap;
            
            foreach (var mgr in managersToUse.Values)
            {
                try
                {
                    mgr.OnWorldEnded();
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }
            }

            
            foreach (var mgr in managersToUse.Values)
            {
                try
                {
                    mgr.OnManagerDestroyed();
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }
            }
        }
    }
}