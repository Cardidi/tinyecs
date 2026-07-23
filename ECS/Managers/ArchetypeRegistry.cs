using System;
using System.Collections.Generic;

namespace CoreECS.Managers
{
    internal sealed class ArchetypeRegistry
    {
        private readonly List<Archetype> m_archetypes = new List<Archetype>();
        private readonly Dictionary<ArchetypeSignature, Archetype> m_bySignature = new Dictionary<ArchetypeSignature, Archetype>();

        public Archetype Empty { get; }

        public ArchetypeRegistry()
        {
            Empty = new Archetype(0, ArchetypeSignature.Empty);
            m_archetypes.Add(Empty);
            m_bySignature[ArchetypeSignature.Empty] = Empty;
        }

        public Archetype GetOrCreate(ArchetypeSignature signature)
        {
            if (m_bySignature.TryGetValue(signature, out var existing))
                return existing;

            var archetype = new Archetype(m_archetypes.Count, signature);
            m_archetypes.Add(archetype);
            m_bySignature[signature] = archetype;
            return archetype;
        }

        public Archetype Get(int id)
        {
            if (id < 0 || id >= m_archetypes.Count)
                throw new ArgumentOutOfRangeException(nameof(id));
            return m_archetypes[id];
        }
    }
}
