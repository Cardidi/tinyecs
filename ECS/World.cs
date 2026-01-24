using TinyECS.Managers;

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
        {
            EntityMatch = GetManager<EntityMatchManager>();
            Entity = GetManager<EntityManager>();
            Component = GetManager<ComponentManager>();
            System = GetManager<SystemManager>();
        }

        protected override void OnStart()
        {}

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
    }
}