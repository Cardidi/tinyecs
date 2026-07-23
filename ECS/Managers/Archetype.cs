using System;
using System.Collections.Generic;
using CoreECS.Defines;

namespace CoreECS.Managers
{
    internal sealed class Archetype
    {
        private const int RowsPerChunk = ComponentChunk.DefaultChunkCapacity;

        private readonly List<ComponentChunk> m_chunks = new List<ComponentChunk>();
        private readonly Action<IComponentRefCore, ulong> m_revisionChanged;

        public int Id { get; }
        public ArchetypeSignature Signature { get; }
        public int ReadLockCount { get; private set; }
        public bool IsReadLocked => ReadLockCount > 0;

        /// <summary>
        /// Chunks owned by this archetype.
        /// </summary>
        public IReadOnlyList<ComponentChunk> Chunks => m_chunks;

        internal Archetype(int id, ArchetypeSignature signature, Action<IComponentRefCore, ulong> revisionChanged = null)
        {
            Id = id;
            Signature = signature;
            m_revisionChanged = revisionChanged;
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

            var newChunk = new ComponentChunk(Signature, RowsPerChunk, m_revisionChanged);
            m_chunks.Add(newChunk);
            return newChunk;
        }

        /// <summary>
        /// Adds an entity row and returns its archetype-global row index.
        /// </summary>
        /// <param name="entityId">Entity to place.</param>
        /// <param name="chunk">Chunk that received the entity.</param>
        /// <param name="localRow">Row index within <paramref name="chunk"/>.</param>
        /// <returns>Archetype-global row index used by <see cref="EntityGraph.Row"/>.</returns>
        internal int AddEntityRow(ulong entityId, out ComponentChunk chunk, out int localRow)
        {
            chunk = GetChunkWithSpace();
            var chunkIndex = m_chunks.IndexOf(chunk);
            localRow = chunk.AddRow(entityId);
            return chunkIndex * RowsPerChunk + localRow;
        }

        /// <summary>
        /// Resolves an archetype-global row index into its owning chunk and local row.
        /// </summary>
        internal void Locate(int globalRow, out ComponentChunk chunk, out int localRow)
        {
            var chunkIndex = globalRow / RowsPerChunk;
            localRow = globalRow % RowsPerChunk;
            chunk = m_chunks[chunkIndex];
        }

        /// <summary>
        /// Removes an entity row via swap-back and reports the new global row of any swapped-in entity.
        /// </summary>
        /// <param name="globalRow">Archetype-global row index to remove.</param>
        /// <param name="movedNewGlobalRow">New global row of the swapped-in entity, or -1 when none moved.</param>
        /// <returns>The entity id that was swapped into the freed row, or 0 when none moved.</returns>
        internal ulong RemoveEntityRow(int globalRow, out int movedNewGlobalRow)
        {
            var chunkIndex = globalRow / RowsPerChunk;
            var localRow = globalRow % RowsPerChunk;
            var chunk = m_chunks[chunkIndex];
            var moved = chunk.RemoveRowSwapBack(localRow);
            movedNewGlobalRow = moved != 0 ? chunkIndex * RowsPerChunk + localRow : -1;
            return moved;
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
