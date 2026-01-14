using System.Runtime.CompilerServices;

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

        public readonly TimePlugin<TWorld> Time = null!;
        
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
            Unsafe.AsRef(ref Time) = GetPlugin<TimePlugin<TWorld>>();
        }
    }
}