using TinyECS.Defines;
using TinyECS.Utils;

// ReSharper disable ForCanBeConvertedToForeach

namespace TinyECS.Managers
{
    
    public interface IEntityMatcher
    {
        public bool IsMatched(ulong mask, IReadOnlyCollection<ComponentRefCore> components);
        
        public IReadOnlyList<ulong> Entities { get; }
    }
    
    public interface IEntitySystem : ISystem
    {
        public IReadOnlyList<IEntityMatcher> Matchers { get; }
        
        public void OnEntityMatched(ulong entityId, IEntityMatcher matcher, int matchIndex);
        
        public void OnEntityClashed(ulong entityId, IEntityMatcher matcher, int matchIndex);
    }
    
    public sealed class EntityMatchManager : IWorldManager
    {

        public IWorld World { get; private set; }
        
        private readonly List<IEntitySystem> m_reactiveSystems = new();

        private readonly List<EntityGraph>[] m_modifiedComponent = { new(), new() };

        public IReadOnlyCollection<IEntitySystem> ReactiveSystems => m_reactiveSystems;

        private void _onEntityGotComponent(EntityGraph entityGraph)
        {
            var mod = m_modifiedComponent[0];
            if (!mod.Contains(entityGraph)) mod.Add(entityGraph);
        }

        private void _onEntityLoseComponent(EntityGraph entityGraph)
        {
            var mod = m_modifiedComponent[0];
            if (!mod.Contains(entityGraph)) mod.Add(entityGraph);
        }

        private void SwapBuffer()
        {
            (m_modifiedComponent[0], m_modifiedComponent[1]) = (m_modifiedComponent[1], m_modifiedComponent[0]);
        }
        
        public void RegisterSystem(IEntitySystem sys)
        {
            if (m_reactiveSystems.Contains(sys)) throw new InvalidOperationException("System has already registered!");
            m_reactiveSystems.Add(sys);
        }

        public void UnregisterSystem(IEntitySystem sys)
        {
            if (m_reactiveSystems.Contains(sys)) throw new InvalidOperationException("System has not been register yet!");
            m_reactiveSystems.Remove(sys);
        }
        
        public void UpdateSystemMatcherForEntity(IEntitySystem system, EntityGraph graph)
        {
            for (var i = 0; i < system.Matchers.Count; i++)
            {
                var match = system.Matchers[i];
                if (match == null) continue;
                
                var list = (List<EntityGraph>) match.Entities;
                var isMatched = match.IsMatched(graph.Mask, graph.RwComponents);
                var entityIdx = list.IndexOf(graph);
                if (isMatched && entityIdx < 0)
                {
                    list.Add(graph);
                    try { system.OnEntityMatched(graph.EntityId, match, i); }
                    catch (Exception e) { Log.Exp(e); }
                }
                else if (!isMatched && entityIdx >= 0)
                {
                    try { system.OnEntityClashed(graph.EntityId, match, i); }
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
        
        

        public void OnManagerCreated(IWorld world)
        {
            var entityManager = world.GetManager<EntityManager>();
            entityManager.OnEntityGotComp.Add(_onEntityGotComponent);
            entityManager.OnEntityLoseComp.Add(_onEntityLoseComponent);
            
            var systemManager = world.GetManager<SystemManager>();
            // systemManager.OnSystemBeginExecute.Add(BeforeTick);
            // systemManager.OnSystemBeginExecute.Add(AfterTick);
        }

        public void OnWorldStarted(IWorld world)
        {
        }

        public void OnWorldEnded(IWorld world)
        {
        }

        public void OnManagerDestroyed(IWorld world)
        {
        }
    }
}