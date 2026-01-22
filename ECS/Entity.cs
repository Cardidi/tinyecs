using System;
using System.Collections.Generic;
using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS
{
    /// <summary>
    /// Represents an entity in the world.
    /// </summary>
    public readonly struct Entity
    {

        #region Internals

        private readonly IWorld m_world;
        
        private readonly ulong m_entityId;

        // Cache entity manager and component manager to avoid querying world multiple times.
        
        private readonly EntityManager m_entityManager;

        private readonly ComponentManager m_componentManager;

        private EntityGraph _accessGraph()
        {
            if (m_entityManager?.EntityCaches.TryGetValue(m_entityId, out var graph) ?? false)
            {
                return graph;
            }
            
            throw new InvalidOperationException("Entity has already been destroyed.");
        }
        
        private ComponentManager _accessComponentManager()
        {
            if (m_componentManager != null && (m_entityManager?.EntityCaches.ContainsKey(m_entityId) ?? false))
            {
                return m_componentManager;
            }
            
            throw new InvalidOperationException("Entity is not associated with any world.");
        }

        #endregion

        public IWorld World => m_world ?? throw new InvalidOperationException("Entity is not associated with any world.");

        public bool IsValid => m_world?.GetManager<EntityManager>().EntityCaches.ContainsKey(m_entityId) ?? false;

        public ulong EntityId => m_entityId;

        public ulong Mask => _accessGraph().Mask;
        
        public ComponentRef<T> CreateComponent<T>() where T : struct, IComponent<T>
        {
            var compRef = _accessComponentManager().CreateComponent<T>(m_entityId);
            return new ComponentRef<T>(compRef);
        }
        
        public ComponentRef CreateComponent(Type componentType)
        {
            var compRef = _accessComponentManager().CreateComponent(m_entityId, componentType);
            return new ComponentRef(compRef);
        }
        
        public void DestroyComponent<T>(ComponentRef<T> comp) where T : struct, IComponent<T>
        {
            _accessComponentManager().DestroyComponent<T>(comp.Core);
        }
        
        public void DestroyComponent(ComponentRef comp)
        {
            _accessComponentManager().DestroyComponent(comp.Core);
        }
        
        public ComponentRef<TComp> GetComponent<TComp>() where TComp : struct, IComponent<TComp>
        {
            return _accessGraph().GetComponent<TComp>();
        }

        public ComponentRef[] GetComponents()
        {
            return _accessGraph().GetComponents();
        }

        public int GetComponents(ICollection<ComponentRef> results)
        {
            return _accessGraph().GetComponents(results);
        }

        public ComponentRef<TComp>[] GetComponents<TComp>() where TComp : struct, IComponent<TComp>
        {
            return _accessGraph().GetComponents<TComp>();
        }

        public int GetComponents<TComp>(ICollection<ComponentRef<TComp>> results) where TComp : struct, IComponent<TComp>
        {
            return _accessGraph().GetComponents(results);
        }

        public Entity(IWorld world, ulong entityId)
        {
            m_world = world;
            m_entityId = entityId;
            m_entityManager = world.GetManager<EntityManager>();
            m_componentManager = world.GetManager<ComponentManager>();
        }
    }
}