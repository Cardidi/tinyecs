using System;
using System.Collections.Generic;

namespace CoreECS.Managers
{
    internal sealed class Archetype
    {
        private readonly List<ComponentChunk> m_chunks = new List<ComponentChunk>();

        public int Id { get; }
        public ArchetypeSignature Signature { get; }
        public int ReadLockCount { get; private set; }
        public bool IsReadLocked => ReadLockCount > 0;

        /// <summary>
        /// Chunks owned by this archetype.
        /// </summary>
        public IReadOnlyList<ComponentChunk> Chunks => m_chunks;

        internal Archetype(int id, ArchetypeSignature signature)
        {
            Id = id;
            Signature = signature;
        }

        /// <summary>
        /// Returns an existing chunk with free row space, or allocates a new chunk.
        /// </summary>
        public ComponentChunk GetChunkWithSpace()
        {
            for (var i = 0; i < m_chunks.Count; i++)
            {
                var chunk = m_chunks[i];
                if (chunk.Count < chunk.Capacity)
                    return chunk;
            }

            var newChunk = new ComponentChunk(Signature);
            m_chunks.Add(newChunk);
            return newChunk;
        }

        public void AddReadLock()
        {
            ReadLockCount++;
        }

        public void RemoveReadLock()
        {
            if (ReadLockCount <= 0)
                throw new InvalidOperationException("Archetype read lock underflow.");
            ReadLockCount--;
        }
    }
}
