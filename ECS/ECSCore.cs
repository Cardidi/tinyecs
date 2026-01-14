namespace TinyECS
{
    #region World Define

    /// <summary>
    /// The minimal define of a world to hold all entities, systems and components.
    /// </summary>
    public interface IWorld<T> where T : class, IWorld<T>
    {
        /// <summary>
        /// Query for all system in this world.
        /// </summary>
        public IEnumerable<ISystem<T>> Systems { get; }
        
        /// <summary>
        /// Query for all allocated entities id in this world.
        /// </summary>
        public IEnumerable<IEntity> Entities { get; }

        /// <summary>
        /// Get a system in this world by type which can offer more effcient acccess.
        /// </summary>
        /// <typeparam name="TSys"></typeparam>
        /// <returns></returns>
        public TSys GetSystem<TSys>() where TSys : ISystem<T>;

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
        /// Get environment data when this world being created.
        /// </summary>
        public TData GetEnvironmentData<TData>(object envKey, TData fallback = default);

        /// <summary>
        /// Get a plugin of this world.
        /// </summary>
        public TPlugin GetPlugin<TPlugin>() where TPlugin : IPlugin<T>;
        
        /// <summary>
        /// Update this world
        /// </summary>
        public void Tick(float dt);
    }

    #endregion

    #region ECS Infrastructure

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

    /// <summary>
    /// The minimal define of a components on entity.
    /// </summary>
    public interface IComponent<TComponent> where TComponent : struct, IComponent<TComponent>
    {
        public void Initialize(ulong entityId) {}

        public void Deinitialize(ulong entityId) {}
    }
    
    /// <summary>
    /// The minimal define of a system to process entities. It can only driven by world.
    /// </summary>
    public interface ISystem<TWorld> where TWorld : class, IWorld<TWorld>
    {
        
        public void OnCreate(TWorld world);

        
        public void OnTick(TWorld world, float dt);

        public void OnDestroy(TWorld world);
    }
    
    /// <summary>
    /// An internal plug-in module which stand at world level which register when world booted. It is the mutter of ecs
    /// which can drive most event in world.
    /// </summary>
    public interface IPlugin<TWorld> where TWorld : class, IWorld<TWorld>
    {
        public void OnConstruct(TWorld world,
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyList<ISystem<TWorld>> systems,
            IReadOnlyDictionary<object, object> envData);
        
    }
    
    #endregion
}