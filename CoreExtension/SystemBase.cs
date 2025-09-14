using TinyECS.Core;

namespace TinyECS.CoreExtension
{
    public abstract class SystemBase<TWorld> : IEntitySystem<TWorld> where TWorld : World<TWorld>
    {
        public IReadOnlyList<IEntityMatcher> Groups { get; private set; }
        
        public TWorld World { get; private set; }
        
        public virtual void OnEntityIncluded(IEntity graph, IEntityMatcher matcher, int matchIndex) {}

        public virtual void OnEntityExcluded(IEntity graph, IEntityMatcher matcher, int matchIndex) {}

        public virtual void OnInitialized(TWorld world)
        {
            World = world;
            var emp = world.GetPlugin<EntityMatcherPlugin<TWorld>>();
            
            if (emp != null)
            {
                Groups = OnCreateGroups() ?? Array.Empty<IEntityMatcher>();
                emp.RegisterSystem(this);
            }
            else
            {
                Groups = Array.Empty<IEntityMatcher>();
            }

        }

        public virtual void OnDeinitialized(TWorld world)
        {
            var emp = world.GetPlugin<EntityMatcherPlugin<TWorld>>();
            if (emp != null)
            {
                emp.UnregisterSystem(this);
            }

            World = null;
            Groups = null;
        }

        protected virtual IEntityMatcher[] OnCreateGroups()
        {
            return null;
        }
        
        public virtual void OnTick(TWorld world, float dt) {}
    }
}