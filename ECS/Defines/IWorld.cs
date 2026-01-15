namespace TinyECS.Defines
{
    /// <summary>
    /// The minimal define of a world to hold all entities, systems and components.
    /// </summary>
    public interface IWorld
    {
        /// <summary>
        /// Query for all system in this world.
        /// </summary>
        public IEnumerable<ISystem> Systems { get; }
        
        /// <summary>
        /// Query for all allocated entities id in this world.
        /// </summary>
        public IEnumerable<IEntity> Entities { get; }

        /// <summary>
        /// Get a system in this world by type which can offer more effcient acccess.
        /// </summary>
        /// <typeparam name="TSys"></typeparam>
        /// <returns></returns>
        public TSys GetSystem<TSys>() where TSys : ISystem;

        /// <summary>
        /// Get entity graph of an entity by its id.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public IEntity GetEntityGraph(ulong entity);
        
        /// <summary>
        /// Query for all components without type constraint.
        /// </summary>
        public ComponentRef[] GetComponents();
        
        /// <summary>
        /// Query for all components without type constraint.
        /// </summary>
        public int GetComponents(ICollection<ComponentRef> result);

        /// <summary>
        /// Query for all components with type constraint.
        /// </summary>
        public ComponentRef<TComp>[] GetComponents<TComp>() where TComp : struct, IComponent<TComp>;

        /// <summary>
        /// Query for all components with type constraint.
        /// </summary>
        public int GetComponents<TComp>(ICollection<ComponentRef<TComp>> result) where TComp : struct, IComponent<TComp>;

        /// <summary>
        /// Query for all managers with type constraint.
        /// </summary>
        public TMgr GetManager<TMgr>() where TMgr : IWorldManager;
    }
}