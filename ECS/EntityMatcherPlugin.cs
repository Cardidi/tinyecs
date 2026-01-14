using TinyECS.Vendor;

// ReSharper disable ForCanBeConvertedToForeach

namespace TinyECS
{
    
    public interface IEntityMatcher
    {
        public bool IsMatched(Entity graph);
        
        public IReadOnlyList<Entity> Entities { get; }
    }
    
    public interface IEntitySystem<TWorld> : ISystem<TWorld> where TWorld : class, IWorld<TWorld>
    {
        public IReadOnlyList<IEntityMatcher> Groups { get; }
        
        public void OnEntityIncluded(IEntity graph, IEntityMatcher matcher, int matchIndex);
        
        public void OnEntityExcluded(IEntity graph, IEntityMatcher matcher, int matchIndex);
    }
    
    public class EntityMatcherPlugin<TWorld> : IPlugin<TWorld> where TWorld :World<TWorld>
    {

        private readonly List<IEntitySystem<TWorld>> m_reactiveSystems = new();

        private readonly List<Entity>[] m_modifiedComponent = { new(), new() };

        public IReadOnlyCollection<IEntitySystem<TWorld>> ReactiveSystems => m_reactiveSystems;
        
        
        public void OnConstruct(
            TWorld world,
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyList<ISystem<TWorld>> systems,
            IReadOnlyDictionary<object, object> envData)
        {
            var egp = world.GetPlugin<EntityPlugin<TWorld>>();
            egp.OnEntityGotComp.Add(EntityGotComp);
            egp.OnEntityLoseComp.Add(EntityLoseComp);
            world.OnBeforeTick.Add(BeforeTick);
            world.OnAfterTick.Add(AfterTick);
        }

        private void BeforeTick(TWorld world, float tick)
        {
            UpdateAllModifications();
        }

        private void AfterTick(TWorld world, float tick)
        {
            UpdateAllModifications();
        }

        private void EntityGotComp(Entity entity)
        {
            var mod = m_modifiedComponent[0];
            if (!mod.Contains(entity)) mod.Add(entity);
        }

        private void EntityLoseComp(Entity entity)
        {
            var mod = m_modifiedComponent[0];
            if (!mod.Contains(entity)) mod.Add(entity);
        }

        private void SwapBuffer()
        {
            (m_modifiedComponent[0], m_modifiedComponent[1]) = (m_modifiedComponent[1], m_modifiedComponent[0]);
        }
        
        public void RegisterSystem(IEntitySystem<TWorld> sys)
        {
            if (m_reactiveSystems.Contains(sys)) throw new InvalidOperationException("System has already registered!");
            m_reactiveSystems.Add(sys);
        }

        public void UnregisterSystem(IEntitySystem<TWorld> sys)
        {
            if (m_reactiveSystems.Contains(sys)) throw new InvalidOperationException("System has not been register yet!");
            m_reactiveSystems.Remove(sys);
        }
        
        public void UpdateSystemMatcherForEntity(IEntitySystem<TWorld> system, Entity graph)
        {
            for (var i = 0; i < system.Groups.Count; i++)
            {
                var match = system.Groups[i];
                if (match == null) continue;
                
                var list = (List<Entity>) match.Entities;
                var isMatched = match.IsMatched(graph);
                var entityIdx = list.IndexOf(graph);
                if (isMatched && entityIdx < 0)
                {
                    list.Add(graph);
                    try { system.OnEntityIncluded(graph, match, i); }
                    catch (Exception e) { Log.Exp(e); }
                }
                else if (!isMatched && entityIdx >= 0)
                {
                    try { system.OnEntityExcluded(graph, match, i); }
                    catch (Exception e) { Log.Exp(e); }
                    list.RemoveAt(entityIdx);
                }
            }
        }

        public void UpdateAllModifications()
        {
            var mods = m_modifiedComponent[0];
            var systems = m_reactiveSystems;
            if (mods.Count == 0) return;
            
            SwapBuffer();
            
            for (var i = 0; i < mods.Count; i++)
            {
                var graph = mods[i];
                for (var j = 0; j < systems.Count; j++)
                {
                    var system = systems[j];
                    UpdateSystemMatcherForEntity(system, graph);
                }
            }
            
            mods.Clear();
        }
    }
}