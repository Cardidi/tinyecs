using System;
using System.Collections;
using System.Collections.Generic;
using CoreECS.Defines;
using CoreECS.Managers;

namespace CoreECS
{
    /// <summary>
    /// Streams entities matching an <see cref="IEntityMatcher"/> from archetype chunk storage.
    /// </summary>
    public sealed class EntityQuery : IEnumerable<Entity>, IDisposable
    {
        private readonly World m_world;
        private readonly IEntityMatcher m_matcher;
        private readonly EntityManager m_entityManager;
        private readonly ComponentManager m_componentManager;

        /// <summary>
        /// Creates a query bound to the specified world managers.
        /// </summary>
        /// <param name="world">World that owns the entities.</param>
        /// <param name="matcher">Matcher used to filter streamed entities.</param>
        /// <param name="entityManager">Entity manager used to validate entity graph state.</param>
        /// <param name="componentManager">Component manager that owns archetype storage.</param>
        internal EntityQuery(World world, IEntityMatcher matcher, EntityManager entityManager, ComponentManager componentManager)
        {
            m_world = world;
            m_matcher = matcher;
            m_entityManager = entityManager;
            m_componentManager = componentManager;
        }

        /// <summary>
        /// Creates an enumerator that read-locks candidate archetypes until disposed.
        /// </summary>
        /// <returns>An enumerator over matching entities.</returns>
        public IEnumerator<Entity> GetEnumerator()
        {
            return new Enumerator(m_world, m_matcher, m_entityManager, m_componentManager);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Disposes this query object. Active enumerators own their read locks independently.
        /// </summary>
        public void Dispose()
        {
        }

        private sealed class Enumerator : IEnumerator<Entity>
        {
            private readonly World m_world;
            private readonly IEntityMatcher m_matcher;
            private readonly EntityManager m_entityManager;
            private readonly ComponentManager m_componentManager;
            private readonly Archetype[] m_archetypes;

            private int m_archetypeIndex;
            private int m_chunkIndex;
            private int m_rowIndex;
            private bool m_disposed;

            public Entity Current { get; private set; }

            object IEnumerator.Current => Current;

            public Enumerator(World world, IEntityMatcher matcher, EntityManager entityManager, ComponentManager componentManager)
            {
                m_world = world;
                m_matcher = matcher;
                m_entityManager = entityManager;
                m_componentManager = componentManager;
                m_archetypes = GetCandidateArchetypes(componentManager, matcher);
                m_archetypeIndex = 0;
                m_chunkIndex = 0;
                m_rowIndex = -1;
                AddReadLocks();
            }

            public bool MoveNext()
            {
                if (m_disposed) return false;

                while (m_archetypeIndex < m_archetypes.Length)
                {
                    var archetype = m_archetypes[m_archetypeIndex];
                    while (m_chunkIndex < archetype.Chunks.Count)
                    {
                        var chunk = archetype.Chunks[m_chunkIndex];
                        m_rowIndex++;

                        while (m_rowIndex < chunk.Count)
                        {
                            if (TryBuildCurrent(archetype, chunk, m_rowIndex))
                                return true;

                            m_rowIndex++;
                        }

                        m_chunkIndex++;
                        m_rowIndex = -1;
                    }

                    m_archetypeIndex++;
                    m_chunkIndex = 0;
                    m_rowIndex = -1;
                }

                Current = default;
                return false;
            }

            public void Reset()
            {
                throw new NotSupportedException("EntityQuery enumerators cannot be reset.");
            }

            public void Dispose()
            {
                if (m_disposed) return;
                m_disposed = true;
                Current = default;
                RemoveReadLocks();
            }

            private bool TryBuildCurrent(Archetype archetype, ComponentChunk chunk, int row)
            {
                var entityId = chunk.EntityIds[row];
                if (entityId == 0) return false;
                if (!m_entityManager.EntityCaches.TryGetValue(entityId, out var graph)) return false;
                if (graph.WishDestroy) return false;
                if ((m_matcher.EntityMask & graph.Mask) == 0) return false;
                if (!m_matcher.Matches(archetype.Signature, chunk.Proxies[row])) return false;

                Current = new Entity(m_world, entityId, graph.Generation, m_entityManager, m_componentManager);
                return true;
            }

            private void AddReadLocks()
            {
                var locked = 0;
                try
                {
                    for (; locked < m_archetypes.Length; locked++)
                        m_archetypes[locked].AddReadLock();
                }
                catch
                {
                    for (var i = locked - 1; i >= 0; i--)
                        m_archetypes[i].RemoveReadLock();
                    throw;
                }
            }

            private void RemoveReadLocks()
            {
                for (var i = m_archetypes.Length - 1; i >= 0; i--)
                    m_archetypes[i].RemoveReadLock();
            }

            private static Archetype[] GetCandidateArchetypes(ComponentManager componentManager, IEntityMatcher matcher)
            {
                var all = componentManager.ArchetypeRegistry.Archetypes;
                var candidates = new List<Archetype>(all.Count);
                for (var i = 0; i < all.Count; i++)
                {
                    var archetype = all[i];
                    if (CouldMatchDenseSignature(matcher, archetype.Signature))
                        candidates.Add(archetype);
                }

                return candidates.ToArray();
            }

            private static bool CouldMatchDenseSignature(IEntityMatcher matcher, ArchetypeSignature signature)
            {
                if (matcher is EntityMatcher entityMatcher)
                    return entityMatcher.CouldMatchDenseSignature(signature);

                return true;
            }
        }
    }
}
