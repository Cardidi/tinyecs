using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyECS.Defines;
using TinyECS.Utils;

namespace TinyECS.Managers
{
    /// <summary>
    /// Mediator to manage world managers.
    /// </summary>
    public sealed class ManagerMediator
    {
        private static readonly Type ParentType = typeof(IWorldManager);
        
        private readonly ImmutableDictionary<Type, IWorldManager> m_managerMap;
        
        private readonly Injector m_injector;

        public ManagerMediator(IWorld world, Injector injector, IReadOnlyList<Type> registeredManagers)
        {
            Assertion.IsNotNull(world);

            m_injector = injector;
            var built = ImmutableDictionary.CreateBuilder<Type, IWorldManager>();
            
            foreach (var mt in registeredManagers)
            {
                if (mt == null)
                {
                    Log.Err($"Null registered manager type founded!");
                    continue;
                }
                
                if (!ParentType.IsAssignableFrom(mt))
                {
                    Log.Err($"Not a valid manager type {mt}");
                    continue;
                }

                if (built.ContainsKey(mt))
                {
                    Log.Err($"Duplicated manager type {mt}");
                    continue;
                }
                
                var manager = (IWorldManager) Activator.CreateInstance(mt, world);
                built.Add(mt, manager);
            }

            m_managerMap = built.ToImmutable();
        }
        
        public bool Booted { get; private set; }
        
        public IReadOnlyDictionary<Type, IWorldManager> Managers => m_managerMap;

        public void Boot()
        {
            if (Booted) return;
            Booted = true;
            
            foreach (var mgr in m_managerMap.Values)
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
            
            foreach (var mgr in m_managerMap.Values)
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

        public void Construct()
        {
            if (m_injector == null) return;
            foreach (var mgr in m_managerMap.Values) m_injector.InjectConstructor(mgr);
        }

        public void Shutdown()
        {
            if (!Booted) return;
            Booted = false;
            
            foreach (var mgr in m_managerMap.Values)
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
            
            foreach (var mgr in m_managerMap.Values)
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