using System;
using System.Collections.Generic;
using CoreECS.Defines;

namespace CoreECS.Managers
{
    internal sealed class ArchetypeRegistry
    {
        private readonly List<Archetype> m_archetypes = new List<Archetype>();
        private readonly Dictionary<ArchetypeSignature, Archetype> m_bySignature = new Dictionary<ArchetypeSignature, Archetype>();
        private readonly Action<IComponentRefCore, ulong> m_revisionChanged;

        public Archetype Empty { get; }

        public ArchetypeRegistry(Action<IComponentRefCore, ulong> revisionChanged = null)
        {
            m_revisionChanged = revisionChanged;
            Empty = new Archetype(0, ArchetypeSignature.Empty, revisionChanged);
            m_archetypes.Add(Empty);
            m_bySignature[ArchetypeSignature.Empty] = Empty;
        }

        public Archetype GetOrCreate(ArchetypeSignature signature)
        {
            if (m_bySignature.TryGetValue(signature, out var existing))
                return existing;

            var archetype = new Archetype(m_archetypes.Count, signature, m_revisionChanged);
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
