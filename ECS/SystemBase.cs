using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS
{
    public abstract class SystemBase<TWorld> : IEntitySystem where TWorld : class, IWorld
    {
        public IReadOnlyList<IEntityMatcher> Matchers { get; private set; }
        
        public TWorld World { get; private set; }
        
        public virtual void OnEntityIncluded(IEntity graph, IEntityMatcher matcher, int matchIndex) {}

        public virtual void OnEntityExcluded(IEntity graph, IEntityMatcher matcher, int matchIndex) {}

        public virtual void OnCreate(TWorld world)
        {
            World = world;
            var emp = world.GetManager<EntityMatchManager>();
            
            if (emp != null)
            {
                Matchers = GetMatchers() ?? Array.Empty<IEntityMatcher>();
                emp.RegisterSystem(this);
            }
            else
            {
                Matchers = Array.Empty<IEntityMatcher>();
            }

        }

        public virtual void OnDestroy(TWorld world)
        {
            var emp = world.GetManager<EntityMatchManager>();
            if (emp != null)
            {
                emp.UnregisterSystem(this);
            }

            World = null;
            Matchers = null;
        }

        protected virtual IEntityMatcher[] GetMatchers()
        {
            return null;
        }
        
        public virtual void OnTick(TWorld world) {}
    }
}