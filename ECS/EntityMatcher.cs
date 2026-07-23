using System;
using System.Collections.Generic;
using CoreECS.Defines;
using CoreECS.Managers;

namespace CoreECS
{
    /// <summary>
    /// Interface for entity matchers that can exclude entities with specific components.
    /// This is the first stage in the fluent interface for building entity queries.
    /// </summary>
    public interface INoneOfEntityMatcher : IEntityMatcher
    {
        /// <summary>
        /// Excludes entities that have the specified component type.
        /// </summary>
        /// <typeparam name="T">Component type to exclude, must be a struct implementing IComponent&lt;T&gt;</typeparam>
        /// <returns>This matcher instance for method chaining</returns>
        public INoneOfEntityMatcher OfNone<T>() where T : struct, IComponent<T>;
    }

    /// <summary>
    /// Interface for entity matchers that can include entities with any of the specified components.
    /// This is the second stage in the fluent interface for building entity queries.
    /// </summary>
    public interface IAnyOfEntityMatcher : INoneOfEntityMatcher
    {
        /// <summary>
        /// Includes entities that have at least one of the specified component types.
        /// </summary>
        /// <typeparam name="T">Component type to include, must be a struct implementing IComponent&lt;T&gt;</typeparam>
        /// <returns>This matcher instance for method chaining</returns>
        public IAnyOfEntityMatcher OfAny<T>() where T : struct, IComponent<T>;
    }
    
    /// <summary>
    /// Interface for entity matchers that can require entities to have all specified components.
    /// This is the final stage in the fluent interface for building entity queries.
    /// </summary>
    public interface IAllOfEntityMatcher : IAnyOfEntityMatcher
    {
        /// <summary>
        /// Requires entities to have all of the specified component types.
        /// </summary>
        /// <typeparam name="T">Component type to require, must be a struct implementing IComponent&lt;T&gt;</typeparam>
        /// <returns>This matcher instance for method chaining</returns>
        public IAllOfEntityMatcher OfAll<T>() where T : struct, IComponent<T>;
    }
    
    /// <summary>
    /// Implementation of the entity matcher that allows building complex queries for entities
    /// based on their component composition. Uses a fluent interface to specify inclusion,
    /// exclusion, and requirement criteria.
    /// </summary>
    public class EntityMatcher : IAllOfEntityMatcher
    {

        #region Config
        
        /// <summary>
        /// Excludes entities that have the specified component type.
        /// </summary>
        /// <typeparam name="T">Component type to exclude, must be a struct implementing IComponent&lt;T&gt;</typeparam>
        /// <returns>This matcher instance for method chaining</returns>
        public INoneOfEntityMatcher OfNone<T>() where T : struct, IComponent<T>
        {
            var type = typeof(T);
            m_none.Add(type);
            if (ComponentStorageKind.IsSparse(type))
                m_noneSparse.Add(type);
            else
                m_noneDense.Add(type);
            return this;
        }

        /// <summary>
        /// Includes entities that have at least one of the specified component types.
        /// </summary>
        /// <typeparam name="T">Component type to include, must be a struct implementing IComponent&lt;T&gt;</typeparam>
        /// <returns>This matcher instance for method chaining</returns>
        public IAnyOfEntityMatcher OfAny<T>() where T : struct, IComponent<T>
        {
            var type = typeof(T);
            m_any.Add(type);
            if (ComponentStorageKind.IsSparse(type))
                m_anySparse.Add(type);
            else
                m_anyDense.Add(type);
            return this;
        }

        /// <summary>
        /// Requires entities to have all of the specified component types.
        /// </summary>
        /// <typeparam name="T">Component type to require, must be a struct implementing IComponent&lt;T&gt;</typeparam>
        /// <returns>This matcher instance for method chaining</returns>
        public IAllOfEntityMatcher OfAll<T>() where T : struct, IComponent<T>
        {
            var type = typeof(T);
            m_all.Add(type);
            if (ComponentStorageKind.IsSparse(type))
                m_allSparse.Add(type);
            else
                m_allDense.Add(type);
            return this;
        }

        /// <summary>
        /// Private constructor for creating an entity matcher with a specific mask.
        /// </summary>
        /// <param name="mask">The entity mask to use for filtering</param>
        private EntityMatcher(ulong mask)
        {
            m_mask = mask;
        }

        /// <summary>
        /// Creates a new entity matcher that matches all entities.
        /// </summary>
        public static EntityMatcher With => new(ulong.MaxValue);

        /// <summary>
        /// Creates a new entity matcher with the specified entity mask.
        /// </summary>
        /// <param name="mask">The entity mask to use for filtering</param>
        /// <returns>A new entity matcher with the specified mask</returns>
        public static EntityMatcher WithMask(ulong mask) => new(mask);
        
        #endregion
        
        /// <summary>
        /// The entity mask used for initial filtering.
        /// </summary>
        private readonly ulong m_mask;
        
        /// <summary>
        /// Set of component types that entities must have all of.
        /// </summary>
        private readonly HashSet<Type> m_all = new();
        
        /// <summary>
        /// Set of component types that entities must have at least one of.
        /// </summary>
        private readonly HashSet<Type> m_any = new();
        
        /// <summary>
        /// Set of component types that entities must not have.
        /// </summary>
        private readonly HashSet<Type> m_none = new();

        /// <summary>
        /// Dense component types required by <see cref="m_all"/>.
        /// </summary>
        private readonly HashSet<Type> m_allDense = new();

        /// <summary>
        /// Sparse component types required by <see cref="m_all"/>.
        /// </summary>
        private readonly HashSet<Type> m_allSparse = new();

        /// <summary>
        /// Dense component types considered by <see cref="m_any"/>.
        /// </summary>
        private readonly HashSet<Type> m_anyDense = new();

        /// <summary>
        /// Sparse component types considered by <see cref="m_any"/>.
        /// </summary>
        private readonly HashSet<Type> m_anySparse = new();

        /// <summary>
        /// Dense component types excluded by <see cref="m_none"/>.
        /// </summary>
        private readonly HashSet<Type> m_noneDense = new();

        /// <summary>
        /// Sparse component types excluded by <see cref="m_none"/>.
        /// </summary>
        private readonly HashSet<Type> m_noneSparse = new();

        /// <summary>
        /// Temporary set used during component filtering.
        /// </summary>
        private readonly HashSet<Type> m_changing = new();

        /// <summary>
        /// Filters an entity based on its components using the configured criteria.
        /// </summary>
        /// <param name="components">Collection of component references for the entity</param>
        /// <returns>True if the entity matches the criteria, false otherwise</returns>
        [Obsolete("Use Matches(ArchetypeSignature, SparseSetProxy) for archetype-backed matching.")]
        public bool ComponentFilter(IReadOnlyCollection<IComponentRefCore> components)
        {
            m_changing.Clear();
    
            // If no "any" criteria specified, consider it satisfied
            bool anyConditionMet = m_any.Count == 0; 
            foreach (var component in components)
            {
                var type = component.RefLocator.GetT();
                
                // If entity has a component that should be excluded, reject it
                if (m_none.Contains(type)) return false;
                
                // If entity has a component that satisfies the "any" criteria, mark it as satisfied
                if (!anyConditionMet && m_any.Contains(type)) anyConditionMet = true;
                
                // Track all component types for the "all" check
                m_changing.Add(type);
            }
    
            // Entity matches if "any" condition is met and it has all required components
            return anyConditionMet && m_changing.IsSupersetOf(m_all);
        }

        /// <summary>
        /// Determines if an entity's dense archetype signature and sparse proxy satisfy the matcher.
        /// </summary>
        /// <param name="denseSignature">Dense component composition for the entity's archetype row</param>
        /// <param name="sparseProxy">Sparse component handles attached to the entity row</param>
        /// <returns>True if the entity matches the criteria, false otherwise</returns>
        public bool Matches(ArchetypeSignature denseSignature, SparseSetProxy sparseProxy)
        {
            foreach (var type in m_noneDense)
            {
                if (denseSignature.Has(type))
                    return false;
            }

            foreach (var type in m_noneSparse)
            {
                if (sparseProxy != null && sparseProxy.Has(type))
                    return false;
            }

            foreach (var type in m_allDense)
            {
                if (!denseSignature.Has(type))
                    return false;
            }

            foreach (var type in m_allSparse)
            {
                if (sparseProxy == null || !sparseProxy.Has(type))
                    return false;
            }

            if (m_anyDense.Count == 0 && m_anySparse.Count == 0)
                return true;

            foreach (var type in m_anyDense)
            {
                if (denseSignature.Has(type))
                    return true;
            }

            foreach (var type in m_anySparse)
            {
                if (sparseProxy != null && sparseProxy.Has(type))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified component type is relevant
        /// to this matcher's criteria (all, any, or none sets).
        /// </summary>
        /// <param name="componentType">The component type to check</param>
        /// <returns>True if the component appears in any matcher set</returns>
        public bool IsRelevantComponent(Type componentType)
        {
            if (m_all.Count == 0 && m_any.Count == 0 && m_none.Count == 0)
                return true;
            return m_all.Contains(componentType) || m_any.Contains(componentType) || m_none.Contains(componentType);
        }

        /// <summary>
        /// Gets the entity mask for this matcher.
        /// </summary>
        public ulong EntityMask => m_mask;
    }
}