using System;
using System.Collections.Generic;
using TinyECS.Defines;
using TinyECS.Utils;

namespace TinyECS.Managers
{

    public delegate void EntityGetComponent(EntityGraph entityGraph);
    
    public delegate void EntityLoseComponent(EntityGraph entityGraph);
    
    /// <summary>
    /// Manages entities in the world.
    /// </summary>
    public sealed class EntityManager : IWorldManager
    {
        public IWorld World { get; private set; }
        
        public Signal<EntityGetComponent> OnEntityGotComp { get; } = new();
        
        public Signal<EntityLoseComponent> OnEntityLoseComp { get; } = new();
        
        private ulong m_allocatedId = 0;

        private bool m_init = false;

        private bool m_shutdown = false;
        
        private readonly Dictionary<ulong, EntityGraph> m_entityCaches = new();

        public IReadOnlyDictionary<ulong, EntityGraph> EntityCaches => m_entityCaches;
                
        public EntityGraph CreateEntity(ulong mask)
        {
            Assertion.IsTrue(m_init);
            Assertion.IsFalse(m_shutdown);
            
            if (m_allocatedId == ulong.MaxValue) throw new ApplicationException(
                "No more entities can being allocated! Please consider restart application...");
            
            var id = ++m_allocatedId;
            var graph = EntityGraph.Pool.Get();
            m_entityCaches.Add(id, graph);
            graph.Mask = mask;
            graph.EntityId = id;
            
            return graph;
        }
        
        public EntityGraph GetEntity(ulong entityId)
        {
            Assertion.IsTrue(m_init);
            Assertion.IsFalse(m_shutdown);
            
            if (m_entityCaches.TryGetValue(entityId, out var graph))
                return graph;
            return null;
        }
        
        public void DestroyEntity(ulong entityId)
        {
            Assertion.IsTrue(m_init);
            Assertion.IsFalse(m_shutdown);
            
            if (m_entityCaches.Remove(entityId, out var graph))
            {
                graph.RwComponents.Clear();
                graph.WishDestroy = true;
                OnEntityLoseComp.Emit(in graph, static (h, c) => h(c));
                EntityGraph.Pool.Release(graph);
            }
        }

        private void _onComponentAdded(ComponentRefCore component, ulong entityId)
        {
            var gs = GetEntity(entityId);
            if (gs == null) return;
            
            gs.RwComponents.Add(component);
            
            OnEntityGotComp.Emit(in gs, static (h, c) => h(c));
        }
        
        private void _onComponentRemoved(ComponentRefCore component, ulong entityId)
        {
            var gs = GetEntity(entityId);
            if (gs == null) return;
            gs.RwComponents.Remove(component);
            
            OnEntityLoseComp.Emit(in gs, static (h, c) => h(c));
        }

        public void OnManagerCreated()
        {
            var compManager = World.GetManager<ComponentManager>();
            compManager.OnComponentCreated.Add(_onComponentAdded);
            compManager.OnComponentRemoved.Add(_onComponentRemoved);

            m_init = true;
        }

        public void OnWorldStarted() {}

        public void OnWorldEnded() {}

        public void OnManagerDestroyed()
        {
            m_shutdown = true;
            
            var compManager = World.GetManager<ComponentManager>();
            compManager.OnComponentCreated.Remove(_onComponentAdded);
            compManager.OnComponentRemoved.Remove(_onComponentRemoved);
            
            foreach (var ec in m_entityCaches.Values)
            {
                ec.RwComponents.Clear();
                EntityGraph.Pool.Release(ec);
            }
            
            m_entityCaches.Clear();
        }
    }
}