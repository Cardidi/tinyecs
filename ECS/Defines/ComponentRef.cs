namespace TinyECS.Defines
{
    
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

        public ComponentRefCore GetRefCore(int offset);
    }
    
    /// <summary>
    /// Readonly component core reference object to holds the component reference.
    /// </summary>
    public class ComponentRefCore
    {
        public IComponentRefLocator RefLocator => m_refLocator;

        public int Offset => m_offset;

        public uint Version => m_version;

        private IComponentRefLocator m_refLocator;

        private int m_offset;

        private uint m_version;

        internal ComponentRefCore(IComponentRefLocator refLocator, int offset, uint version)
        {
            m_refLocator = refLocator;
            m_offset = offset;
            m_version = version;
        }

        public void RewriteRef(IComponentRefLocator locator, int offset, uint version)
        {
            m_refLocator = locator;
            m_offset = offset;
            m_version = version;
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
        public ref T R
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

}