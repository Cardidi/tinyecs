namespace TinyECS.Core
{

    public class WorldBase : WorldBase<WorldBase>
    {
        public WorldBase(WorldBuilder<WorldBase> builder) : base(builder)
        {
        }
    }
    
    public abstract class WorldBase<TWorld> : World<TWorld> where TWorld : WorldBase<TWorld>
    {

        public TimePlugin<TWorld> Time => m_time;

        private TimePlugin<TWorld> m_time;
        
        protected WorldBase(WorldBuilder<TWorld> builder) : base(builder)
        {
            builder
                .InstallPlugin<EntityMatcherPlugin<TWorld>>()
                .InstallPlugin<TimePlugin<TWorld>>();
        }

        public override void OnPreBuilt(
            IReadOnlyList<ISystem<TWorld>> systems,
            IReadOnlyList<IPlugin<TWorld>> plugins, 
            IReadOnlyDictionary<object, object> envData)
        {
            m_time = GetPlugin<TimePlugin<TWorld>>();
        }
    }
}