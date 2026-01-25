using System;
using TinyECS.Defines;
using TinyECS.Managers;
using TinyECS.Utils;

namespace TinyECS
{
    /// <summary>
    /// A basic usable world which contains minimal managers to run the ECS system.
    /// </summary>
    public class World : MinimalWorld
    {
        protected EntityMatchManager EntityMatch { get; private set; }
        
        protected EntityManager Entity { get; private set; }

        protected ComponentManager Component { get; private set; }

        protected SystemManager System { get; private set; }
        
        protected override void OnRegisterManager(IManagerRegister register)
        {
            register.RegisterManager<EntityMatchManager>();
            register.RegisterManager<EntityManager>();
            register.RegisterManager<ComponentManager>();
            register.RegisterManager<SystemManager>();
        }

        protected override void OnConstruct()
        {}

        protected override void OnStart()
        {
            EntityMatch = GetManager<EntityMatchManager>();
            Entity = GetManager<EntityManager>();
            Component = GetManager<ComponentManager>();
            System = GetManager<SystemManager>();
        }

        protected override void OnTickBegin()
        {
            System.TeardownSystems();
        }

        protected override void OnTick(ulong tickMask)
        {
            System.ExecuteSystems(tickMask);
        }

        protected override void OnTickEnd()
        {
            System.CleanupSystems();
        }

        protected override void OnShutdown()
        {}
        
        /// <summary>
        /// Finds a system of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of system to find, must implement ISystem</typeparam>
        /// <returns>The system instance if found, otherwise default(T)</returns>
        public T FindSystem<T>() where T : class, ISystem
        {
            Assertion.IsTrue(Ready, "World is not ready");
            
            if (System != null && System.SystemTransformer.TryGetValue(typeof(T), out var system))
            {
                return (T)system;
            }
            
            return null;
        }

        #region PublicAPI

        /// <summary>
        /// Creates a new entity in the world.
        /// </summary>
        /// <returns>A new Entity instance</returns>
        public Entity CreateEntity(ulong mask = ulong.MaxValue)
        {
            Assertion.IsTrue(Ready, "World is not ready");
            
            if (Entity != null && Component != null)
            {
                var entityGraph = Entity.CreateEntity(mask); // Default mask
                return new Entity(this, entityGraph.EntityId, Entity, Component);
            }
            
            throw new InvalidOperationException("Core ECS managers are not available");
        }
        
        /// <summary>
        /// Destroys an entity by its ID.
        /// </summary>
        /// <param name="entityId">The ID of the entity to destroy</param>
        public void DestroyEntity(ulong entityId)
        {
            Assertion.IsTrue(Ready, "World is not ready");

            if (Entity == null || Component == null)
                throw new InvalidOperationException("Core ECS managers are not available");
            
            Entity.DestroyEntity(entityId);
        }
        
        /// <summary>
        /// Destroys an entity.
        /// </summary>
        /// <param name="entity">The entity to destroy</param>
        public void DestroyEntity(Entity entity)
        {
            Assertion.IsTrue(Ready, "World is not ready");
            
            if (entity.IsValid)
            {
                DestroyEntity(entity.EntityId);
            }
        }

        /// <summary>
        /// Registers a system with the world.
        /// </summary>
        /// <param name="systemType">The type of system to register</param>
        public void RegisterSystem(Type systemType)
        {
            Assertion.IsTrue(Ready, "World is not ready");

            if (System != null)
            {
                System.RegisterSystem(systemType);
            } else
            {
                throw new InvalidOperationException("System manager is not available");
            }
        }

        /// <summary>
        /// Registers a system with the world.
        /// </summary>
        /// <typeparam name="T">The type of system to register, must implement ISystem</typeparam>
        public void RegisterSystem<T>() where T : class, ISystem
        {
            Assertion.IsTrue(Ready, "World is not ready");
            
            if (System != null)
            {
                System.RegisterSystem(typeof(T));
            }
            else
            {
                throw new InvalidOperationException("System manager is not available");
            }
        }

        /// <summary>
        /// Unregisters a system from the world.
        /// </summary>
        /// <param name="systemType">The type of system to unregister</param>
        public void UnregisterSystem(Type systemType)
        {
            Assertion.IsTrue(Ready, "World is not ready");
            
            if (System != null)
            {
                System.UnregisterSystem(systemType);
            } else
            {
                throw new InvalidOperationException("System manager is not available");
            }
        }
        
        /// <summary>
        /// Unregisters a system from the world.
        /// </summary>
        /// <typeparam name="T">The type of system to unregister, must implement ISystem</typeparam>
        public void UnregisterSystem<T>() where T : class, ISystem
        {
            Assertion.IsTrue(Ready, "World is not ready");
            
            if (System != null)
            {
                System.UnregisterSystem(typeof(T));
            }
            else
            {
                throw new InvalidOperationException("System manager is not available");
            }
        }

        /// <summary>
        /// Creates a new entity collector with the specified matcher and flag.
        /// </summary>
        /// <param name="matcher">The entity matcher to use for filtering entities</param>
        /// <param name="flag">The flag to use for the collector</param>
        /// <returns>A new IEntityCollector instance</returns>
        public IEntityCollector CreateCollector(IEntityMatcher matcher,
            EntityCollectorFlag flag = EntityCollectorFlag.None)
        {
            Assertion.IsTrue(Ready, "World is not ready");

            if (EntityMatch != null)
            {
                return EntityMatch.MakeCollector(flag, matcher);
            }
            else
            {
                throw new InvalidOperationException("EntityMatch manager is not available");
            }
        }

        #endregion
    }
}