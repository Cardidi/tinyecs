using System;
using System.Collections.Generic;
using TinyECS.Defines;
using TinyECS.Managers;

namespace TinyECS
{
    public readonly struct Entity
    {

        private readonly EntityManager m_mgr;
        
        private readonly ulong m_entityId;

        private EntityGraph _accessGraph()
        {
            if (m_mgr != null && m_mgr.EntityCaches.TryGetValue(m_entityId, out var graph))
            {
                return graph;
            }
            
            throw new InvalidOperationException("Entity has already been destroyed.");
        }
        
        public bool IsValid
        {
            get
            {
                if (m_mgr != null)
                {
                    return m_mgr.EntityCaches.ContainsKey(m_entityId);
                }

                return false;
            }
        }

        public ulong EntityId => m_entityId;
        
        public ComponentRef<TComp> GetComponent<TComp>() where TComp : struct, IComponent<TComp>
        {
            throw new NotImplementedException();
        }

        public ComponentRef[] GetComponents()
        {
            throw new NotImplementedException();
        }

        public int GetComponents(ICollection<ComponentRef> results)
        {
            throw new NotImplementedException();
        }

        public ComponentRef<TComp>[] GetComponents<TComp>() where TComp : struct, IComponent<TComp>
        {
            throw new NotImplementedException();
        }

        public int GetComponents<TComp>(ICollection<ComponentRef<TComp>> results) where TComp : struct, IComponent<TComp>
        {
            throw new NotImplementedException();
        }
    }
}