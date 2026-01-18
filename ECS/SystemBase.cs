using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS
{
    public abstract class SystemBase<TWorld> : IEntitySystem where TWorld : class, IWorld
    {
        public IReadOnlyList<IEntityMatcher> Matchers { get; private set; }

        public virtual ulong TickGroup => ulong.MaxValue;
        
        public IWorld World { get; private set; }

        public virtual void OnEntityMatched(ulong entityId, IEntityMatcher matcher, int matchIndex) {}

        public virtual void OnEntityClashed(ulong entityId, IEntityMatcher matcher, int matchIndex) {}

        public virtual void OnCreate()
        {
            var emp = World.GetManager<EntityMatchManager>();
            
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

        public virtual void OnDestroy()
        {
            var emp = World.GetManager<EntityMatchManager>();
            if (emp != null)
            {
                emp.UnregisterSystem(this);
            }
        }

        protected virtual IEntityMatcher[] GetMatchers()
        {
            return null;
        }
        
        public virtual void OnTick() {}
    }
}