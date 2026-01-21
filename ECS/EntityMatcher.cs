using TinyECS.Defines;
using TinyECS.Managers;

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

        #region Config
        
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

        private EntityMatcher(ulong mask)
        {
            m_mask = mask;
        }

        public static EntityMatcher With => new(ulong.MaxValue);

        public static EntityMatcher WithMask(ulong mask) => new(mask);
        
        #endregion
        
        private readonly ulong m_mask;
        
        private readonly HashSet<Type> m_all = new();
        
        private readonly HashSet<Type> m_any = new();
        
        private readonly HashSet<Type> m_none = new();

        private readonly HashSet<Type> m_changing = new();

        public bool ComponentFilter(IReadOnlyCollection<ComponentRefCore> components)
        {
            m_changing.Clear();
    
            bool anyConditionMet = m_any.Count == 0; 
            foreach (var component in components)
            {
                var type = component.RefLocator.GetT();
                if (m_none.Contains(type)) return false;
                if (!anyConditionMet && m_any.Contains(type)) anyConditionMet = true;
                m_changing.Add(type);
            }
    
            return anyConditionMet && m_changing.IsSupersetOf(m_all);
        }

        public ulong EntityMask => m_mask;
    }
}