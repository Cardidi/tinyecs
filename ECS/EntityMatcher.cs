using TinyECS.Defines;
using TinyECS.Managers;
using TinyECS.Utils;

namespace TinyECS
{

    public interface INoneOfEntityMatcher : IEntityMatcher
    {
        public INoneOfEntityMatcher OfNone<T>() where T : struct, IComponent<T>;
    }

    public interface IAnyOfEntityMatcher : INoneOfEntityMatcher
    {
        public IAnyOfEntityMatcher OfAny<T>() where T : struct, IComponent<T>;
    }
    
    
    public interface IAllOfEntityMatcher : IAnyOfEntityMatcher
    {
        public IAllOfEntityMatcher OfAll<T>() where T : struct, IComponent<T>;
    }
    
    public class EntityMatcher : IAllOfEntityMatcher
    {
        private ulong m_mask;

        private bool m_allowEmpty;
        
        private HashSet<Type> m_all = new();
        
        private HashSet<Type> m_any = new();
        
        private HashSet<Type> m_none = new();

        private List<ulong> m_collected = new();
        
        public bool IsMatched(ulong mask, IReadOnlyCollection<ComponentRefCore> components)
        {
            // Test if mask is matched
            if (m_mask != 0 && (mask & m_mask) == 0) return false;
            
            // Test if empty entity is allowed.
            if (!m_allowEmpty && components.Count == 0) return false;
            
            using (HashSetPool<Type>.Get(out var all))
            {
                var isAny = m_any.Count == 0;
                foreach (var input in components.Select(x => x.RefLocator.GetT()))
                {
                    if (m_none.Contains(input)) return false;
                    if (!isAny && m_any.Contains(input)) isAny = true;
                    all.Add(input);
                }

                return isAny && all.IsSupersetOf(m_all);
            }
        }

        public IReadOnlyList<ulong> Entities => m_collected;
        
        public INoneOfEntityMatcher OfNone<T>() where T : struct, IComponent<T>
        {
            m_none.Add(typeof(T));
            return this;
        }

        public IAnyOfEntityMatcher OfAny<T>() where T : struct, IComponent<T>
        {
            m_any.Add(typeof(T));
            return this;
        }

        public IAllOfEntityMatcher OfAll<T>() where T : struct, IComponent<T>
        {
            m_all.Add(typeof(T));
            return this;
        }

        public EntityMatcher AllowEmpty()
        {
            m_allowEmpty = true;
            return this;
        }

        private EntityMatcher(ulong mask)
        {
            m_mask = mask;
        }

        public static EntityMatcher With => new(0);

        public static EntityMatcher WithMask(ulong mask)
        {
            return new EntityMatcher(mask);
        }
    }
}