namespace TinyECS.Defines
{
    /// <summary>
    /// This is a graph based object that represents the relationship of entities and components. Each entity will
    /// hold a graph like this and cache their components with type info at here. By this graph, ECS can boost the
    /// speed of calculations such as match or query on components.
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// The only-one id allocate for this entity.
        /// </summary>
        public ulong EntityId { get; }
        
        /// <summary>
        /// Mask code of this entity which can be used on testify type of entity.
        /// </summary>
        public ulong Mask { get; }
        
        /// <summary>
        /// Get first component of this entity.
        /// </summary>
        public ComponentRef<TComp> GetComponent<TComp>() where TComp : struct, IComponent<TComp>;
        
        /// <summary>
        /// Get all components of this entity.
        /// </summary>
        public ComponentRef[] GetComponents();
        
        /// <summary>
        /// Get all components of this entity.
        /// </summary>
        public int GetComponents(ICollection<ComponentRef> results);

        /// <summary>
        /// Get specific type of components of this entity.
        /// </summary>
        public ComponentRef<TComp>[] GetComponents<TComp>() where TComp : struct, IComponent<TComp>;
        
        /// <summary>
        /// Get specific type of components of this entity.
        /// </summary>
        public int GetComponents<TComp>(ICollection<ComponentRef<TComp>> results) where TComp : struct, IComponent<TComp>;
    }
}