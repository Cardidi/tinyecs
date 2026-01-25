using System;
using System.Collections.Generic;
using TinyECS.Defines;
using TinyECS.Utils;

namespace TinyECS.Managers
{
    /// <summary>
    /// Defines a matcher to filter entities based on their components.
    /// </summary>
    public interface IEntityMatcher
    {
        /// <summary>
        /// Determines if an entity satisfies all requirements of the matcher.
        /// </summary>
        /// <param name="components">All components of this entity</param>
        /// <returns>True if the entity matches the criteria, false otherwise</returns>
        public bool ComponentFilter(IReadOnlyCollection<IComponentRefCore> components);

        /// <summary>
        /// Gets the allowed entities mask for this matcher.
        /// </summary>
        public ulong EntityMask { get; }
    }

    /// <summary>
    /// Collects entities that satisfy a matcher's criteria.
    /// </summary>
    public interface IEntityCollector : IDisposable
    {
        /// <summary>
        /// Gets the matcher of this collector.
        /// </summary>
        public IEntityMatcher Matcher { get; }

        /// <summary>
        /// Gets all collected entities.
        /// </summary>
        public IReadOnlyList<ulong> Collected { get; }
        
        /// <summary>
        /// Gets all collected entities with realtime matching.
        /// Do not use foreach on this field as it may change during iteration!
        /// </summary>
        public IReadOnlyList<ulong> RealtimeCollected { get; }

        /// <summary>
        /// Gets entities that were previously excluded from collector and are now being collected.
        /// </summary>
        public IReadOnlyList<ulong> Matching { get; }

        /// <summary>
        /// Gets entities that were previously included in collector and are now being excluded.
        /// </summary>
        public IReadOnlyList<ulong> Clashing { get; }

        /// <summary>
        /// Summarizes previous changes and starts a new collecting phase.
        /// </summary>
        public void Change();
    }

    /// <summary>
    /// Flags that control the behavior of entity collectors.
    /// </summary>
    [Flags]
    public enum EntityCollectorFlag
    {
        /// <summary>
        /// No special behavior.
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Don't remove elements from RealtimeCollected before Change() is called.
        /// </summary>
        LazyRemoval = 1 << 0,
    }

    /// <summary>
    /// Manages entity collectors and matching logic.
    /// This class is responsible for creating and updating collectors based on entity changes.
    /// </summary>
    public sealed class EntityMatchManager : IWorldManager
    {
        /// <summary>
        /// Internal implementation of IEntityCollector that manages multiple buffers for efficient entity tracking.
        /// </summary>
        private class Collector : IEntityCollector
        {
            // Buffers:
            // [0] = collected
            // [1] = matching
            // [2] = clashing
            // [3] = realtime collected
            // [4] = change matching
            // [5] = change clashing
            public readonly List<ulong>[] Buffers = new[]
            {
                new List<ulong>(),
                new List<ulong>(),
                new List<ulong>(),
                new List<ulong>(),
                new List<ulong>(),
                new List<ulong>(),
            };
            
            /// <summary>
            /// Gets the flags for this collector.
            /// </summary>
            public EntityCollectorFlag Flag { get; }

            /// <summary>
            /// Gets the matcher for this collector.
            /// </summary>
            public IEntityMatcher Matcher { get; }

            /// <summary>
            /// Gets the collected entities buffer.
            /// </summary>
            public IReadOnlyList<ulong> Collected => Buffers[0];

            /// <summary>
            /// Gets the realtime collected entities buffer.
            /// </summary>
            public IReadOnlyList<ulong> RealtimeCollected => Buffers[3];

            /// <summary>
            /// Gets the matching entities buffer.
            /// </summary>
            public IReadOnlyList<ulong> Matching => Buffers[1];

            /// <summary>
            /// Gets the clashing entities buffer.
            /// </summary>
            public IReadOnlyList<ulong> Clashing => Buffers[2];

            /// <summary>
            /// Gets a value indicating whether this collector has been destroyed.
            /// </summary>
            public bool Destroyed { get; private set; } = false;

            /// <summary>
            /// Summarizes previous changes and starts a new collecting phase.
            /// </summary>
            public void Change()
            {
                // Swap buffers: [0] <- [3], [1] <- [4], [2] <- [5], [3] <- [0], [4] <- [1], [5] <- [2]
                (Buffers[0], Buffers[1], Buffers[2], Buffers[3], Buffers[4], Buffers[5]) = 
                    (Buffers[3], Buffers[4], Buffers[5], Buffers[0], Buffers[1], Buffers[2]);
                
                // Clear previous matching and clashing buffers
                Buffers[4].Clear();
                Buffers[5].Clear();
                
                // Copy data from back to front
                var processRemoval = (Flag & EntityCollectorFlag.LazyRemoval) > 0;
                var changedMatch = Buffers[1];
                var changedClash = Buffers[2];

                // Must do a removal at the end of match and start of change
                if (processRemoval && changedClash.Count > 0)
                {
                    var frontBuffer = Buffers[0];
                    var changed = 0;

                    // Apply clashing
                    for (int i = 0; i < frontBuffer.Count - changed; i++)
                    {
                        var id = frontBuffer[i];
                        if (!changedClash.Contains(id)) continue;

                        changed += 1;
                        (frontBuffer[i], frontBuffer[^changed]) = (frontBuffer[^changed], frontBuffer[i]);
                    }
                    
                    // Shrink array
                    if (changed > 0)
                    {
                        frontBuffer.RemoveRange(frontBuffer.Count - changed, changed);
                    }
                }

                // Update back buffer to ensure alignment with front buffer
                if (changedMatch.Count + changedClash.Count > 0)
                {
                    var backBuffer = Buffers[3];
                    backBuffer.Clear();
                    backBuffer.AddRange(Buffers[0]);
                }
            }

            /// <summary>
            /// Releases all resources used by the collector.
            /// Clears all buffers and removes the collector from the EntityMatchManager.
            /// </summary>
            public void Dispose()
            {
                Destroyed = true;
                
                // Clear all buffers
                for (var i = 0; i < Buffers.Length; i++)
                {
                    Buffers[i].Clear();
                }
                
                // Remove this collector from the manager's list
                m_manager._onDisposeCollector(this);
            }

            /// <summary>
            /// Initializes a new instance of the Collector class.
            /// </summary>
            /// <param name="matcher">The matcher to use for filtering entities</param>
            /// <param name="flag">The flags that control collector behavior</param>
            /// <param name="manager">The manager that created this collector</param>
            public Collector(IEntityMatcher matcher, EntityCollectorFlag flag, EntityMatchManager manager)
            {
                Matcher = matcher;
                Flag = flag;
                m_manager = manager;
            }

            /// <summary>
            /// Reference to the manager that created this collector.
            /// </summary>
            private readonly EntityMatchManager m_manager;
        }

        /// <summary>
        /// Gets the world this manager belongs to.
        /// </summary>
        public IWorld World { get; }

        /// <summary>
        /// Reference to the entity manager for tracking entity changes.
        /// </summary>
        private EntityManager m_entityManager;

        /// <summary>
        /// List of all collectors managed by this manager.
        /// </summary>
        private readonly List<Collector> m_collectors = new();

        /// <summary>
        /// Handles component addition events.
        /// </summary>
        /// <param name="entityGraph">The entity graph that changed</param>
        private void _onComponentAdded(EntityGraph entityGraph)
        {
            _onEntityChanged(entityGraph, true);
        }

        /// <summary>
        /// Handles component removal events.
        /// </summary>
        /// <param name="entityGraph">The entity graph that changed</param>
        private void _onComponentRemoved(EntityGraph entityGraph)
        {
            _onEntityChanged(entityGraph, false);
        }

        /// <summary>
        /// Handles entity changes by updating all collectors.
        /// </summary>
        /// <param name="entityGraph">The entity graph that changed</param>
        /// <param name="isAdd">True if components were added, false if removed</param>
        private void _onEntityChanged(EntityGraph entityGraph, bool isAdd)
        {
            foreach (var collector in m_collectors)
            {
                _changeCollector(collector, entityGraph, isAdd, false);
            }
        }

        /// <summary>
        /// Updates a collector based on entity changes.
        /// </summary>
        /// <param name="collector">The collector to update</param>
        /// <param name="entityGraph">The entity graph that changed</param>
        /// <param name="isAdd">True if components were added, false if removed</param>
        /// <param name="init">True if this is during initialization</param>
        private void _changeCollector(Collector collector, EntityGraph entityGraph, bool isAdd, bool init)
        {
            var matcher = collector.Matcher;
            // Quick-pass filter
            if ((matcher.EntityMask & entityGraph.Mask) == 0) return;
            
            // Buffers
            var collectBuffer = collector.Buffers[3];
            var matchBuffer = collector.Buffers[4];
            var clashBuffer = collector.Buffers[5];

            // Config
            var dontRemove = (collector.Flag & EntityCollectorFlag.LazyRemoval) > 0;
            var entityId = entityGraph.EntityId;
            
            // If lazy removal, it will not represent in collectBuffer.
            var alreadyCollected = !init && collectBuffer.Contains(entityId) && (!dontRemove || clashBuffer.Contains(entityId));
            
            var isMatched = !entityGraph.WishDestroy && matcher.ComponentFilter(entityGraph.RwComponents);
            
            // Unchanged then do nothing
            if (!(isMatched ^ alreadyCollected)) return;
                
            if (isMatched)
            {
                collectBuffer.Add(entityId);
                if (!clashBuffer.Remove(entityId)) matchBuffer.Add(entityId);
            }
            else
            {
                if (!dontRemove) collectBuffer.Remove(entityId);
                if (!matchBuffer.Remove(entityId)) clashBuffer.Add(entityId);
            }
        }

        /// <summary>
        /// Removes a collector from the manager's list.
        /// </summary>
        /// <param name="collector">The collector to remove</param>
        private bool _onDisposeCollector(Collector collector)
        {
            return m_collectors.Remove(collector);
        }
        
        /// <summary>
        /// Creates a new entity collector with the specified matcher.
        /// </summary>
        /// <param name="matcher">The matcher to use for filtering entities</param>
        /// <returns>A new entity collector</returns>
        public IEntityCollector MakeCollector(IEntityMatcher matcher)
        {
            return MakeCollector(EntityCollectorFlag.None, matcher);
        }

        /// <summary>
        /// Creates a new entity collector with the specified matcher and flags.
        /// </summary>
        /// <param name="flag">Flags that control collector behavior</param>
        /// <param name="matcher">The matcher to use for filtering entities</param>
        /// <returns>A new entity collector</returns>
        public IEntityCollector MakeCollector(EntityCollectorFlag flag, IEntityMatcher matcher)
        {
            Assertion.IsNotNull(matcher);
            
            var c = new Collector(matcher, flag, this);
            m_collectors.Add(c);

            var entityManager = World.GetManager<EntityManager>();
            foreach (var ec in entityManager.EntityCaches.Values)
            {
                _changeCollector(c, ec, false, true);
            }

            return c;
        }

        /// <summary>
        /// Called when the manager is created.
        /// </summary>
        public void OnManagerCreated()
        {
            m_entityManager.OnEntityGotComp.Add(_onComponentAdded);
            m_entityManager.OnEntityLoseComp.Add(_onComponentRemoved);
        }

        /// <summary>
        /// Called when the world starts.
        /// </summary>
        public void OnWorldStarted()
        {
        }

        /// <summary>
        /// Called when the world ends.
        /// </summary>
        public void OnWorldEnded()
        {
        }

        /// <summary>
        /// Called when the manager is destroyed.
        /// </summary>
        public void OnManagerDestroyed()
        {
            foreach (var collector in m_collectors)
            {
                for (var i = 0; i < collector.Buffers.Length; i++)
                {
                    var buf = collector.Buffers[i];
                    collector.Buffers[i] = null;
                    buf.Clear();
                }
            }
            
            m_collectors.Clear();

            m_entityManager.OnEntityGotComp.Remove(_onComponentAdded);
            m_entityManager.OnEntityLoseComp.Remove(_onComponentRemoved);
        }

        /// <summary>
        /// Initializes a new instance of the EntityMatchManager class.
        /// </summary>
        /// <param name="world">The world this manager belongs to</param>
        /// <param name="entityManager">The entity manager for tracking entity changes</param>
        public EntityMatchManager(IWorld world, EntityManager entityManager)
        {
            World = world;
            m_entityManager = entityManager;
        }
    }
}