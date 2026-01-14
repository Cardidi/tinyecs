using TinyECS.Vendor;

namespace TinyECS
{

    public delegate void EntityGetCompHandler(Entity entity);
    
    public delegate void EntityLoseCompHandler(Entity entity);
    
    public class EntityPlugin<TWorld> : IPlugin<TWorld> where TWorld : World<TWorld>
    {
        public Signal<EntityGetCompHandler> OnEntityGotComp { get; } = new();
        
        public Signal<EntityLoseCompHandler> OnEntityLoseComp { get; } = new();
        
        private ulong m_allocatedId = 0;
        
        private readonly Dictionary<ulong, Entity> m_entities = new();

        private readonly HashSet<Entity> m_removals = new();

        private readonly HashSet<Entity> m_preservedEntityGraph = new();

        public IReadOnlyDictionary<ulong, Entity> Entities => m_entities;
        
        public void OnConstruct(TWorld world, 
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyList<ISystem<TWorld>> systems, 
            IReadOnlyDictionary<object, object> envData)
        {
            var comp = world.GetPlugin<ComponentPlugin<TWorld>>();
            comp.OnCompAdded.Add(OnCompAdded);
            comp.OnCompRemoved.Add(OnCompRemoved);
            world.OnDestroyTick.Add((_, _) => ReleaseEmptyEntityGraph(), 10);
        }

        public bool IsEntityPreserved(ulong entityId)
        {
            if (!m_entities.TryGetValue(entityId, out var graph))
            {
                return false;
            }

            return m_preservedEntityGraph.Contains(graph);
        }

        public bool SetEntityPreserved(ulong entityId, bool preserved)
        {
            if (!m_entities.TryGetValue(entityId, out var graph))
            {
                return false;
            }

            var changed = preserved ? m_preservedEntityGraph.Add(graph) : m_preservedEntityGraph.Remove(graph);
            if (changed)
            {
                if (preserved) m_removals.Remove(graph);
                else if (graph.RwComponents.Count == 0) m_removals.Add(graph);
            }
            
            return changed;
        }
                
        public Entity RequestEntityGraph(ulong mask)
        {
            var id = ++m_allocatedId;
            var graph = Entity.Pool.Get();
            m_entities.Add(id, graph);
            graph.Mask = mask;
            graph.EntityId = id;
            
            return graph;
        }
        
        public Entity GetEntityGraph(ulong entityId)
        {
            if (m_entities.TryGetValue(entityId, out var graph))
                return graph;
            return null;
        }

        public int ReleaseEmptyEntityGraph()
        {
            using (ListPool<Entity>.Get(out var removals))
            {
                removals.AddRange(m_removals);
                m_removals.Clear();
                
                for (var i = 0; i < removals.Count; i++)
                {
                    var eg = removals[i];
                    
                    OnEntityLoseComp.Emit(in eg, static (h, c) => h(c));
                    Entity.Pool.Release(eg);
                }

                return removals.Count;
            }
        }
        
        private void OnCompAdded(ComponentRefCore component, ulong entityId)
        {
            var gs = GetEntityGraph(entityId);
            if (gs == null) return;
            
            gs.RwComponents.Add(component);
            m_removals.Remove(gs);
            
            OnEntityGotComp.Emit(in gs, static (h, c) => h(c));
        }
        
        private void OnCompRemoved(ComponentRefCore component, ulong entityId)
        {
            var gs = GetEntityGraph(entityId);
            if (gs == null) return;
            gs.RwComponents.Remove(component);
            if (gs.RwComponents.Count == 0 && !m_preservedEntityGraph.Contains(gs)) m_removals.Add(gs);
            
            OnEntityLoseComp.Emit(in gs, static (h, c) => h(c));
        }
    }
}