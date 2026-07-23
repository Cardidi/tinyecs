using System;
using System.Collections.Generic;
using CoreECS.Defines;
using CoreECS.Utils;

namespace CoreECS.Managers
{
    /// <summary>
    /// Delegate for entity component acquisition events.
    /// </summary>
    /// <param name="entityGraph">The entity graph that acquired a component</param>
    /// <param name="componentType">The type of the component that was added</param>
    public delegate void EntityGetComponent(EntityGraph entityGraph, Type componentType);
    
    /// <summary>
    /// Delegate for entity component loss events.
    /// </summary>
    /// <param name="entityGraph">The entity graph that lost a component</param>
    /// <param name="componentType">The type of the component that was removed</param>
    public delegate void EntityLoseComponent(EntityGraph entityGraph, Type componentType);

    /// <summary>
    /// Delegate for entity component revision change events.
    /// </summary>
    /// <param name="entityGraph">The entity graph whose component changed</param>
    /// <param name="componentType">The type of the component that changed</param>
    public delegate void EntityChangeComponent(EntityGraph entityGraph, Type componentType);
    
    /// <summary>
    /// Manages entities in the world.
    /// This class is responsible for creating, destroying, and tracking entities in the ECS system.
    /// </summary>
    public sealed class EntityManager : IWorldManager
    {
        /// <summary>
        /// Gets the world this manager belongs to.
        /// </summary>
        public IWorld World { get; }
        
        /// <summary>
        /// Event triggered when an entity gets a component.
        /// </summary>
        public Signal<EntityGetComponent> OnEntityGotComp { get; } = new();
        
        /// <summary>
        /// Event triggered when an entity loses a component.
        /// </summary>
        public Signal<EntityLoseComponent> OnEntityLoseComp { get; } = new();

        /// <summary>
        /// Event triggered when one of an entity's components changes revision.
        /// </summary>
        public Signal<EntityChangeComponent> OnEntityChangeComp { get; } = new();
        
        /// <summary>
        /// The next available entity ID.
        /// </summary>
        private ulong m_allocatedId = 0;

        /// <summary>
        /// Indicates whether the manager has been initialized.
        /// </summary>
        private bool m_init = false;

        /// <summary>
        /// Indicates whether the manager is shutting down.
        /// </summary>
        private bool m_shutdown = false;
        
        /// <summary>
        /// Reference to the component manager for handling component events.
        /// </summary>
        private ComponentManager m_compManager;
        
        /// <summary>
        /// Dictionary mapping entity IDs to their entity graphs.
        /// </summary>
        private readonly Dictionary<ulong, EntityGraph> m_entityCaches = new();

        /// <summary>
        /// Temporary composition index used until archetype chunk routing owns component membership.
        /// </summary>
        private readonly Dictionary<ulong, List<IComponentRefCore>> m_entityComponents = new();

        /// <summary>
        /// Gets a read-only view of the entity caches.
        /// </summary>
        public IReadOnlyDictionary<ulong, EntityGraph> EntityCaches => m_entityCaches;
        
        /// <summary>
        /// Creates a new entity with the specified mask.
        /// </summary>
        /// <param name="mask">The component mask for the new entity</param>
        /// <returns>The entity graph for the newly created entity</returns>
        /// <exception cref="ApplicationException">Thrown when the maximum number of entities has been reached</exception>
        public EntityGraph CreateEntity(ulong mask)
        {
            Assertion.IsTrue(m_init);
            Assertion.IsFalse(m_shutdown);
            
            if (m_allocatedId == ulong.MaxValue) throw new ApplicationException(
                "No more entities can being allocated! Please consider restart application...");
            
            var id = ++m_allocatedId;
            var graph = EntityGraph.Pool.Get();
            m_entityCaches.Add(id, graph);
            m_entityComponents.Add(id, new List<IComponentRefCore>());
            graph.Mask = mask;
            graph.EntityId = id;
            graph.WishDestroy = false;

            m_compManager.PlaceNewEntity(graph);

            return graph;
        }
        
        /// <summary>
        /// Gets the entity graph for the specified entity ID.
        /// </summary>
        /// <param name="entityId">The ID of the entity to retrieve</param>
        /// <returns>The entity graph for the specified entity, or null if not found</returns>
        public EntityGraph GetEntity(ulong entityId)
        {
            Assertion.IsTrue(m_init);
            Assertion.IsFalse(m_shutdown);
            
            if (m_entityCaches.TryGetValue(entityId, out var graph))
                return graph;
            
            return null;
        }
        
        /// <summary>
        /// Destroys the entity with the specified ID.
        /// </summary>
        /// <param name="entityId">The ID of the entity to destroy</param>
        public void DestroyEntity(ulong entityId)
        {
            Assertion.IsTrue(m_init);
            Assertion.IsFalse(m_shutdown);
            
            if (m_entityCaches.TryGetValue(entityId, out var graph))
            {
                graph.WishDestroy = true;
                if (m_entityComponents.TryGetValue(entityId, out var components))
                {
                    var componentsToDestroy = components.ToArray();
                    for (var i = 0; i < componentsToDestroy.Length; i++)
                    {
                        m_compManager.DestroyComponent(componentsToDestroy[i]);
                    }
                }

                m_compManager.RemoveEntityRow(graph);

                m_entityCaches.Remove(entityId);
                m_entityComponents.Remove(entityId);

                OnEntityLoseComp.Emit(in graph, (Type)null, static (h, g, t) => h(g, t));
                EntityGraph.Pool.Release(graph);
            }
        }

        /// <summary>
        /// Gets the component core references attached to the specified entity.
        /// </summary>
        /// <param name="entityId">The entity id to inspect</param>
        /// <returns>Read-only component core references for matcher and entity access</returns>
        internal IReadOnlyCollection<IComponentRefCore> GetComponentCores(ulong entityId)
        {
            if (m_entityComponents.TryGetValue(entityId, out var components)) return components;
            return Array.Empty<IComponentRefCore>();
        }

        /// <summary>
        /// Gets the first component of type TComp attached to the specified entity.
        /// </summary>
        internal ComponentRef<TComp> GetComponent<TComp>(ulong entityId) where TComp : struct, IComponent<TComp>
        {
            if (!m_entityComponents.TryGetValue(entityId, out var components)) return default;
            
            for (var i = 0; i < components.Count; i++)
            {
                var r = components[i];
                var loc = r.RefLocator;
                if (loc.IsT(typeof(TComp))) return new ComponentRef<TComp>(r);
            }

            return default;
        }

        /// <summary>
        /// Gets all components attached to the specified entity.
        /// </summary>
        internal ComponentRef[] GetComponents(ulong entityId)
        {
            if (!m_entityComponents.TryGetValue(entityId, out var components)) return Array.Empty<ComponentRef>();
            
            var result = new ComponentRef[components.Count];
            for (var i = 0; i < components.Count; i++) result[i] = new ComponentRef(components[i]);

            return result;
        }

        /// <summary>
        /// Gets all components attached to the specified entity and appends them to the specified collection.
        /// </summary>
        internal int GetComponents(ulong entityId, ICollection<ComponentRef> results)
        {
            if (!m_entityComponents.TryGetValue(entityId, out var components)) return 0;
            
            for (var i = 0; i < components.Count; i++) results.Add(new ComponentRef(components[i]));

            return components.Count;
        }

        /// <summary>
        /// Gets all components of type TComp attached to the specified entity.
        /// </summary>
        internal ComponentRef<TComp>[] GetComponents<TComp>(ulong entityId) where TComp : struct, IComponent<TComp>
        {
            if (!m_entityComponents.TryGetValue(entityId, out var components)) return Array.Empty<ComponentRef<TComp>>();
            
            using (ListPool<ComponentRef<TComp>>.Get(out var builder))
            {
                for (var i = 0; i < components.Count; i++)
                {
                    var r = components[i];
                    var loc = r.RefLocator;
                    if (loc.IsT(typeof(TComp))) builder.Add(new ComponentRef<TComp>(r));
                }

                return builder.ToArray();
            }
        }

        /// <summary>
        /// Gets all components of type TComp attached to the specified entity and appends them to the specified collection.
        /// </summary>
        internal int GetComponents<TComp>(ulong entityId, ICollection<ComponentRef<TComp>> results)
            where TComp : struct, IComponent<TComp>
        {
            if (!m_entityComponents.TryGetValue(entityId, out var components)) return 0;
            
            var collected = 0;
            for (var i = 0; i < components.Count; i++)
            {
                var r = components[i];
                var loc = r.RefLocator;
                if (loc.IsT(typeof(TComp)))
                {
                    collected += 1;
                    results.Add(new ComponentRef<TComp>(r));
                }
            }

            return collected;
        }

        /// <summary>
        /// Checks whether the specified entity has a component of type TComp.
        /// </summary>
        internal bool HasComponent<TComp>(ulong entityId) where TComp : struct, IComponent<TComp>
        {
            return GetComponent<TComp>(entityId).NotNull;
        }

        /// <summary>
        /// Counts the instances of type TComp attached to the specified entity.
        /// </summary>
        internal int GetComponentCount<TComp>(ulong entityId) where TComp : struct, IComponent<TComp>
        {
            if (!m_entityComponents.TryGetValue(entityId, out var components)) return 0;

            var count = 0;
            for (var i = 0; i < components.Count; i++)
            {
                var loc = components[i].RefLocator;
                if (loc != null && loc.IsT(typeof(TComp))) count += 1;
            }

            return count;
        }

        /// <summary>
        /// Resolves an entity id to its graph without lifecycle assertions, used as the component manager's resolver.
        /// </summary>
        /// <param name="entityId">Entity id to resolve.</param>
        /// <returns>The entity graph, or null when absent.</returns>
        private EntityGraph ResolveEntityGraph(ulong entityId)
        {
            return m_entityCaches.TryGetValue(entityId, out var graph) ? graph : null;
        }

        /// <summary>
        /// Handles component addition events.
        /// </summary>
        /// <param name="component">The component that was added</param>
        /// <param name="entityId">The ID of the entity that received the component</param>
        private void _onComponentAdded(IComponentRefCore component, ulong entityId, Type compType)
        {
            var gs = GetEntity(entityId);
            if (gs == null) return;
            
            m_entityComponents[entityId].Add(component);
            
            OnEntityGotComp.Emit(in gs, compType, static (h, g, t) => h(g, t));
        }
        
        /// <summary>
        /// Handles component removal events.
        /// </summary>
        /// <param name="component">The component that was removed</param>
        /// <param name="entityId">The ID of the entity that lost the component</param>
        private void _onComponentRemoved(IComponentRefCore component, ulong entityId, Type compType)
        {
            var gs = GetEntity(entityId);
            if (gs == null) return;
            if (m_entityComponents.TryGetValue(entityId, out var components))
                components.Remove(component);
            
            OnEntityLoseComp.Emit(in gs, compType, static (h, g, t) => h(g, t));
        }

        /// <summary>
        /// Handles component revision change events.
        /// </summary>
        /// <param name="component">The component that changed</param>
        /// <param name="entityId">The ID of the entity that owns the component</param>
        private void _onComponentChanged(IComponentRefCore component, ulong entityId, Type compType)
        {
            if (!OnEntityChangeComp.HasReceivers) return;

            var gs = GetEntity(entityId);
            if (gs == null) return;

            OnEntityChangeComp.Emit(in gs, compType, static (h, g, t) => h(g, t));
        }

        /// <summary>
        /// Called when the manager is created.
        /// </summary>
        public void OnManagerCreated()
        {
            m_compManager.OnComponentCreated.Add(_onComponentAdded);
            m_compManager.OnComponentRemoved.Add(_onComponentRemoved);
            m_compManager.OnComponentChanged.Add(_onComponentChanged);
            m_compManager.BindEntityGraphResolver(ResolveEntityGraph);

            m_init = true;
        }

        /// <summary>
        /// Called when the world starts.
        /// </summary>
        public void OnWorldStarted() {}

        /// <summary>
        /// Called when the world ends.
        /// </summary>
        public void OnWorldEnded() {}

        /// <summary>
        /// Called when the manager is destroyed.
        /// </summary>
        public void OnManagerDestroyed()
        {
            m_shutdown = true;
            
            m_compManager.OnComponentCreated.Remove(_onComponentAdded);
            m_compManager.OnComponentRemoved.Remove(_onComponentRemoved);
            m_compManager.OnComponentChanged.Remove(_onComponentChanged);
            
            foreach (var ec in m_entityCaches.Values)
            {
                EntityGraph.Pool.Release(ec);
            }
            
            m_entityCaches.Clear();
            m_entityComponents.Clear();
        }

        /// <summary>
        /// Initializes a new instance of the EntityManager class.
        /// </summary>
        /// <param name="world">The world this manager belongs to</param>
        /// <param name="compManager">The component manager for handling component events</param>
        public EntityManager(IWorld world, ComponentManager compManager)
        {
            World = world;
            m_compManager = compManager;
        }
    }
}