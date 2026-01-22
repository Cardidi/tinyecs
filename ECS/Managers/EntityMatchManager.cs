using System;
using System.Collections.Generic;
using TinyECS.Defines;
using TinyECS.Utils;


namespace TinyECS.Managers
{

    public interface IEntityMatcher
    {
        /// <summary>
        /// Judge if an entity satisfy all requirements of matcher.
        /// </summary>
        /// <param name="components">All components of this entity</param>
        /// <returns>Is matched or not.</returns>
        public bool ComponentFilter(IReadOnlyCollection<ComponentRefCore> components);

        /// <summary>
        /// Allowed entities mask
        /// </summary>
        public ulong EntityMask { get; }
    }

    public interface IEntityCollector
    {
        /// <summary>
        /// The matcher of this collector.
        /// </summary>
        public IEntityMatcher Matcher { get; }

        /// <summary>
        /// All collected entities.
        /// </summary>
        public IReadOnlyList<ulong> Collected { get; }
        
        /// <summary>
        /// All collected entities and will use realtime entities matching. Do not use foreach on this field!
        /// </summary>
        public IReadOnlyList<ulong> RealtimeCollected { get; }

        /// <summary>
        /// Entities that previously excluded from collector and being collected after previous change.
        /// </summary>
        public IReadOnlyList<ulong> Matching { get; }

        /// <summary>
        /// Entities that previously included in collector and being excluded after previous change.
        /// </summary>
        public IReadOnlyList<ulong> Clashing { get; }

        /// <summary>
        /// Summary previous changes and start a new collecting phase.
        /// </summary>
        public void Change();

    }


    [Flags]
    public enum EntityCollectorFlag
    {
        /// <summary>
        /// Do nothing special.
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Don't remove element from RealtimeCollected before change.
        /// </summary>
        LazyRemoval = 1 << 0,
    }

    public sealed class EntityMatchManager : IWorldManager
    {

        private class Collector : IEntityCollector
        {

            // buffers:
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
            
            public EntityCollectorFlag Flag { get; }

            public IEntityMatcher Matcher { get; }

            public IReadOnlyList<ulong> Collected => Buffers[0];

            public IReadOnlyList<ulong> RealtimeCollected => Buffers[3];

            public IReadOnlyList<ulong> Matching => Buffers[1];

            public IReadOnlyList<ulong> Clashing => Buffers[2];

            public void Change()
            {
                
                (Buffers[0], Buffers[1], Buffers[2], Buffers[3], Buffers[4], Buffers[5]) = 
                    (Buffers[3], Buffers[4], Buffers[5], Buffers[0], Buffers[1], Buffers[2]);
                
                // Clear prev matching and clashing
                
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

                // Update back buffer to ensure align to front buffer
                
                if (changedMatch.Count + changedClash.Count > 0)
                {
                    var backBuffer = Buffers[3];
                    
                    // Judge use which algorithm to update backend buffer
                    // if ((changedClash.Count * changedClash.Count) < backBuffer.Count)
                    // {
                    //     var changed = 0;
                    //
                    //     // Apply clashing
                    //     for (int i = 0; i < backBuffer.Count - changed; i++)
                    //     {
                    //         var id = backBuffer[i];
                    //         if (!changedClash.Contains(id)) continue;
                    //
                    //         changed += 1;
                    //         (backBuffer[i], backBuffer[^changed]) = (backBuffer[^changed], backBuffer[i]);
                    //     }
                    //
                    //     // Apply matching
                    //     var offset = backBuffer.Count - changed;
                    //     var finalSize = backBuffer.Count - changed + changedMatch.Count;
                    //     backBuffer.EnsureCapacity(finalSize);
                    //     for (var i = 0; i < changedMatch.Count; i++)
                    //     {
                    //         var bufferIdx = offset + i;
                    //         if (bufferIdx >= backBuffer.Count)
                    //         {
                    //             backBuffer.Add(changedMatch[i]);
                    //         }
                    //         else
                    //         {
                    //             backBuffer[bufferIdx] = changedMatch[i];
                    //         }
                    //     }
                    //
                    //     // Shrink array
                    //     var shrinkSize = backBuffer.Count - finalSize;
                    //     if (shrinkSize > 0)
                    //     {
                    //         backBuffer.RemoveRange(finalSize, shrinkSize);
                    //     }
                    // }
                    // else
                    // {
                    backBuffer.Clear();
                    backBuffer.AddRange(Buffers[0]);
                    // }
                }
                
            }

            public Collector(IEntityMatcher matcher, EntityCollectorFlag flag)
            {
                Matcher = matcher;
                Flag = flag;
            }
        }

        public IWorld World { get; private set; }

        private readonly List<Collector> m_collectors = new();

        private void _onComponentAdded(EntityGraph entityGraph)
        {
            _onEntityChanged(entityGraph, true);
        }

        private void _onComponentRemoved(EntityGraph entityGraph)
        {
            _onEntityChanged(entityGraph, false);
        }

        private void _onEntityChanged(EntityGraph entityGraph, bool isAdd)
        {
            foreach (var collector in m_collectors)
            {
                _changeCollector(collector, entityGraph, isAdd, false);
            }
        }

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


        public IEntityCollector MakeCollector(IEntityMatcher matcher)
        {
            return MakeCollector(EntityCollectorFlag.None, matcher);
        }

        public IEntityCollector MakeCollector(EntityCollectorFlag flag, IEntityMatcher matcher)
        {
            Assertion.IsNotNull(matcher);
            
            var c = new Collector(matcher, flag);
            m_collectors.Add(c);

            var entityManager = World.GetManager<EntityManager>();
            foreach (var ec in entityManager.EntityCaches.Values)
            {
                _changeCollector(c, ec, false, true);
            }

            return c;
        }

        public void OnManagerCreated()
        {
            var entityManager = World.GetManager<EntityManager>();
            entityManager.OnEntityGotComp.Add(_onComponentAdded);
            entityManager.OnEntityLoseComp.Add(_onComponentRemoved);
        }

        public void OnWorldStarted()
        {
        }

        public void OnWorldEnded()
        {
        }

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

            var entityManager = World.GetManager<EntityManager>();
            entityManager.OnEntityGotComp.Remove(_onComponentAdded);
            entityManager.OnEntityLoseComp.Remove(_onComponentRemoved);
        }
    }
}
