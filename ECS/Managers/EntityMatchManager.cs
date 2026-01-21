using TinyECS.Defines;


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
                
                var changedMatch = Buffers[1];
                var changedClash = Buffers[2];
                var backBuffer = Buffers[3];

                if (changedMatch.Count + changedClash.Count > 0)
                {
                    // Judge use which algorithm to update backend buffer
                    if ((changedClash.Count * changedClash.Count) < backBuffer.Count)
                    {
                        var changed = 0;

                        // Apply clashing
                        for (int i = 0; i < backBuffer.Count - changed; i++)
                        {
                            var id = backBuffer[i];
                            if (!changedClash.Contains(id)) continue;

                            changed += 1;
                            (backBuffer[i], backBuffer[^changed]) = (backBuffer[^changed], backBuffer[i]);
                        }

                        // Apply matching
                        var offset = backBuffer.Count - changed;
                        backBuffer.EnsureCapacity(backBuffer.Count - changed + changedMatch.Count);
                        for (var i = 0; i < changedMatch.Count; i++)
                        {
                            var bufferIdx = offset + i;
                            if (bufferIdx >= backBuffer.Count)
                            {
                                backBuffer.Add(changedMatch[i]);
                            }
                            else
                            {
                                backBuffer[bufferIdx] = changedMatch[i];
                            }
                        }
                    }
                    else
                    {
                        backBuffer.Clear();
                        backBuffer.AddRange(Buffers[0]);
                    }
                }
                
            }

            public Collector(IEntityMatcher matcher)
            {
                Matcher = matcher;
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
            
            var collectBuffer = collector.Buffers[3];
            var matchBuffer = collector.Buffers[4];
            var clashBuffer = collector.Buffers[5];
                    
            var entityId = entityGraph.EntityId;
            var isMatched = !entityGraph.WishDestroy && matcher.ComponentFilter(entityGraph.RwComponents);
            var alreadyCollected = !init && collectBuffer.Contains(entityId);
            
            // Unchanged then do nothing
            if (!(isMatched ^ alreadyCollected)) return;
                
            if (isMatched)
            {
                collectBuffer.Add(entityId);
                if (!clashBuffer.Remove(entityId)) matchBuffer.Add(entityId);
            }
            else
            {
                collectBuffer.Remove(entityId);
                if (!matchBuffer.Remove(entityId)) clashBuffer.Add(entityId);
            }
        }

        public IEntityCollector MakeCollector(IEntityMatcher matcher)
        {
            var c = new Collector(matcher);
            m_collectors.Add(c);

            var entityManager = World.GetManager<EntityManager>();
            foreach (var ec in entityManager.EntityCaches.Values)
            {
                _changeCollector(c, ec, false, true);
            }

            return c;
        }

        public void OnManagerCreated(IWorld world)
        {
            var entityManager = world.GetManager<EntityManager>();
            entityManager.OnEntityGotComp.Add(_onComponentAdded);
            entityManager.OnEntityLoseComp.Add(_onComponentRemoved);
        }

        public void OnWorldStarted(IWorld world)
        {
        }

        public void OnWorldEnded(IWorld world)
        {
        }

        public void OnManagerDestroyed(IWorld world)
        {
            m_collectors.Clear();

            var entityManager = world.GetManager<EntityManager>();
            entityManager.OnEntityGotComp.Remove(_onComponentAdded);
            entityManager.OnEntityLoseComp.Remove(_onComponentRemoved);
        }
    }
}
