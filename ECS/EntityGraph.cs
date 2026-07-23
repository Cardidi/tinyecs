using CoreECS.Utils;

namespace CoreECS
{
    /// <summary>
    /// Represents the current state of an entity, including identity, lifecycle, and storage location metadata.
    /// Note: Do not cache instances of this class in production as they are pooled and reused.
    /// </summary>
    public sealed class EntityGraph 
    {
        /// <summary>
        /// Object pool for EntityGraph instances to reduce memory allocations.
        /// </summary>
        public static readonly Pool<EntityGraph> Pool = new(
            createFunc: () => new EntityGraph(),
            returnAction: x => x.Reset());

        /// <summary>
        /// Gets the generation (version) of this EntityGraph instance.
        /// This value is incremented each time the EntityGraph is invalidated (returned to pool),
        /// allowing detection of stale references. Cycles between 0 and uint.MaxValue.
        /// </summary>
        public uint Generation { get; private set; }

        /// <summary>
        /// Gets or sets the unique identifier for the entity this graph represents.
        /// </summary>
        public ulong EntityId { get; set; }

        /// <summary>
        /// Gets or sets the component mask for this entity.
        /// The mask is a bitmask that represents which component types this entity has.
        /// </summary>
        public ulong Mask { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether this entity is marked for destruction.
        /// </summary>
        public bool WishDestroy { get; set; }

        /// <summary>
        /// Gets or sets the archetype id that owns this entity's component composition.
        /// The empty archetype uses id 0.
        /// </summary>
        public int ArchetypeId { get; set; }

        /// <summary>
        /// Gets or sets the row within the owning archetype chunk storage.
        /// A value of -1 means the entity is not currently placed in chunk rows.
        /// </summary>
        public int Row { get; set; } = -1;

        /// <summary>
        /// Resets this EntityGraph instance to its default state.
        /// This method is called when the instance is returned to the pool.
        /// Increments the generation to invalidate any stale Entity references.
        /// </summary>
        private void Reset()
        {
            EntityId = 0;
            Mask = 0;
            WishDestroy = false;
            ArchetypeId = 0;
            Row = -1;
            Generation = (Generation % uint.MaxValue) + 1;
        }

        /// <summary>
        /// Private constructor to prevent direct instantiation.
        /// Use the Pool property to get instances.
        /// </summary>
        private EntityGraph() {}
    }
}