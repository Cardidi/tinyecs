using TinyECS.Defines;
using TinyECS.Utils;

namespace TinyECS
{
    /// <summary>
    /// Entity state which can represent the current state of entity. But do not cache this in production because it can
    /// being reused.
    /// </summary>
    public sealed class EntityGraph 
    {

        public static readonly Pool<EntityGraph> Pool = new(
            createFunc: () => new EntityGraph(),
            getAction: x => x.Reset());

        public ulong EntityId { get; set; }

        public ulong Mask { get; set; }
        
        public bool WishDestroy { get; set; }
        

        public ComponentRef<TComp> GetComponent<TComp>() where TComp : struct, IComponent<TComp>
        {
            for (var i = 0; i < RwComponents.Count; i++)
            {
                var r = RwComponents[i];
                var loc = r.RefLocator;
                if (loc.IsT(typeof(TComp))) return new ComponentRef<TComp>(r);
            }

            return default;
        }

        public ComponentRef[] GetComponents()
        {
            return RwComponents.Select(x => new ComponentRef(x)).ToArray();
        }

        public int GetComponents(ICollection<ComponentRef> results)
        {
            var l = RwComponents.Count;
            for (var i = 0; i < RwComponents.Count; i++) results.Add(new ComponentRef(RwComponents[i]));

            return l;
        }

        public ComponentRef<TComp>[] GetComponents<TComp>() where TComp : struct, IComponent<TComp>
        {
            using (ListPool<ComponentRef<TComp>>.Get(out var builder))
            {
                for (var i = 0; i < RwComponents.Count; i++)
                {
                    var r = RwComponents[i];
                    var loc = r.RefLocator;
                    if (loc.IsT(typeof(TComp))) builder.Add(new ComponentRef<TComp>(r));
                }

                return builder.ToArray();
            }
        }

        public int GetComponents<TComp>(ICollection<ComponentRef<TComp>> results)
            where TComp : struct, IComponent<TComp>
        {
            var collected = 0;
            for (var i = 0; i < RwComponents.Count; i++)
            {
                var r = RwComponents[i];
                var loc = r.RefLocator;
                if (loc.IsT(typeof(TComp)))
                {
                    collected += 1;
                    results.Add(new ComponentRef<TComp>(r));
                }
            }

            return collected;
        }

        internal List<ComponentRefCore> RwComponents { get; } = new();

        private void Reset()
        {
            EntityId = 0;
            Mask = 0;
            WishDestroy = false;
            RwComponents.Clear();
        }
        
        private EntityGraph() {}
    }
}