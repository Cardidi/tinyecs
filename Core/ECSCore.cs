namespace TinyECS.Core
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
        public void Init(ulong entityId) {}

        public void Deinit(ulong entityId) {}
    }
    
    /// <summary>
    /// The minimal define of a system to process entities. It can only driven by world.
    /// </summary>
    public interface ISystem<TWorld> where TWorld : class, IWorld<TWorld>
    {
        
        public void OnInitialized(TWorld world);

        
        public void OnTick(TWorld world, float dt);

        public void OnDeinitialized(TWorld world);
    }
    
    /// <summary>
    /// An internal plug-in module which stand at world level which register when world booted. It is the mutter of ecs
    /// which can drive most event in world.
    /// </summary>
    public interface IPlugin<TWorld> where TWorld : class, IWorld<TWorld>
    {
        public void OnBuilt(TWorld world,
            IReadOnlyList<IPlugin<TWorld>> plugins,
            IReadOnlyList<ISystem<TWorld>> systems,
            IReadOnlyDictionary<object, object> envData);
        
    }

    #endregion

    #region Component Access Utility
    
    /// <summary>
    /// Interface define of a component reference memory locator.
    /// </summary>
    public interface IComponentRefLocator
    {
        public bool NotNull(uint version, int offset);

        public ref T Get<T>(int offset) where T : struct, IComponent<T>;

        public bool IsT(Type type);

        public Type GetT();

        public ulong GetEntityId(int offset);

        public ComponentRefCore ReverseCore(int offset);
    }

    /// <summary>
    /// Readonly component core reference object to holds the component reference.
    /// </summary>
    public class ComponentRefCore
    {
        public readonly IComponentRefLocator RefLocator;
        
        public readonly int Offset;
        
        public readonly uint Version;

        internal ComponentRefCore(IComponentRefLocator refLocator, int offset, uint version)
        {
            RefLocator = refLocator;
            Offset = offset;
            Version = version;
        }

        public void RewriteRef(IComponentRefLocator locator, int offset, uint version)
        {
            Unsafe.AsRef(in RefLocator) = locator;
            Unsafe.AsRef(in Offset) = offset;
            Unsafe.AsRef(in Version) = version;
        }
    }

    /// <summary>
    /// Accessor of ComponentRefCore which is typeless reference.
    /// </summary>
    public readonly struct ComponentRef : IEquatable<ComponentRef>
    {
        internal readonly ComponentRefCore Core;

        /// <summary>
        /// Check if this component reference is invalid.
        /// </summary>
        public bool NotNull => Core?.RefLocator != null && Core.RefLocator.NotNull(Core.Version, Core.Offset);

        /// <summary>
        /// Get the real type underlying this component reference.
        /// </summary>
        /// <returns></returns>
        public Type RuntimeType
        {
            get
            {
                if (Core?.RefLocator == null || !Core.RefLocator.NotNull(Core.Version, Core.Offset)) return null;
                return Core.RefLocator.GetT();
            }
        }

        /// <summary>
        /// Get entity id of this component.
        /// </summary>
        public ulong EntityId
        {
            get
            {
                if (Core?.RefLocator == null || !Core.RefLocator.NotNull(Core.Version, Core.Offset)) return 0;
                return Core.RefLocator.GetEntityId(Core.Offset);
            }
        }

        /// <summary>
        /// Check if internal type is matched with give type.
        /// </summary>
        public bool Inspect<T>() where T : struct, IComponent<T>
        {
            if (Core?.RefLocator == null || !Core.RefLocator.NotNull(Core.Version, Core.Offset)) return false;
            return Core.RefLocator.IsT(typeof(T));
        }
        
        /// <summary>
        /// Check if internal type is matched with give type.
        /// </summary>
        public bool Inspect(Type type)
        {
            if (Core?.RefLocator == null || !Core.RefLocator.NotNull(Core.Version, Core.Offset)) return false;
            return Core.RefLocator.IsT(type);
        }

        /// <summary>
        /// Shrink typeless component reference into typed component reference
        /// </summary>
        /// <param name="noSafeCheck">Perform safety check on casting.</param>
        public ComponentRef<T> Shrink<T>(bool noSafeCheck = false) where T : struct, IComponent<T>
        {
            if (noSafeCheck || Core?.RefLocator != null && Core.RefLocator.NotNull(Core.Version, Core.Offset))
            {
                if (noSafeCheck || Core.RefLocator.IsT(typeof(T))) return new ComponentRef<T>(Core);
                throw new InvalidCastException("Given type is unmatched with actual component type.");
            }

            throw new NullReferenceException("Component Reference is cut.");
        }
        
        internal ComponentRef(ComponentRefCore core)
        {
            Core = core;
        }

        public bool Equals(ComponentRef other)
        {
            return Equals(Core, other.Core);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return !NotNull;
            return obj is ComponentRef other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Core != null ? Core.GetHashCode() : 0);
        }

        public static bool operator ==(ComponentRef left, ComponentRef right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ComponentRef left, ComponentRef right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Accessor of ComponentRefCore which is typed reference.
    /// </summary>
    public readonly struct ComponentRef<T> : IEquatable<ComponentRef<T>> where T : struct, IComponent<T>
    {
        internal readonly ComponentRefCore Core;
        
        /// <summary>
        /// Check if this component reference is invalid.
        /// </summary>
        public bool NotNull => Core?.RefLocator != null && Core.RefLocator.NotNull(Core.Version, Core.Offset);

        /// <summary>
        /// Get entity id of this component.
        /// </summary>
        public ulong EntityId
        {
            get
            {
                if (Core?.RefLocator == null || !Core.RefLocator.NotNull(Core.Version, Core.Offset)) return 0;
                return Core.RefLocator.GetEntityId(Core.Offset);
            }
        }

        /// <summary>
        /// Access component data directly.
        /// </summary>
        public ref T RW
        {
            get 
            {
                if (Core?.RefLocator == null || !Core.RefLocator.NotNull(Core.Version, Core.Offset))
                    throw new NullReferenceException("Component Reference is cut.");
            
                return ref Core.RefLocator.Get<T>(Core.Offset);
            }
        }
        
        /// <summary>
        /// Access to a copy of component data.
        /// </summary>
        public ref readonly T RO
        {
            get 
            {
                if (Core?.RefLocator == null || !Core.RefLocator.NotNull(Core.Version, Core.Offset))
                    throw new NullReferenceException("Component Reference is cut.");
            
                return ref Core.RefLocator.Get<T>(Core.Offset);
            }
        }
        
        /// <summary>
        /// Expand typed component reference into typeless component reference
        /// </summary>
        public ComponentRef Expand()
        {
            if (Core?.RefLocator != null && Core.RefLocator.NotNull(Core.Version, Core.Offset))
                return new ComponentRef(Core);

            throw new NullReferenceException("Component Reference is cut.");
        }

        internal ComponentRef(ComponentRefCore core)
        {
            Core = core;
        }
        
        public bool Equals(ComponentRef<T> other)
        {
            return Equals(Core, other.Core);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return !NotNull;
            return obj is ComponentRef<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Core != null ? Core.GetHashCode() : 0);
        }

        public static bool operator ==(ComponentRef<T> left, ComponentRef<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ComponentRef<T> left, ComponentRef<T> right)
        {
            return !left.Equals(right);
        }

        public static implicit operator ComponentRef(ComponentRef<T> obj)
        {
            return obj.Expand();
        }

        public static explicit operator ComponentRef<T>(ComponentRef obj)
        {
            return obj.Shrink<T>();
        }
    }

    #endregion
}