using System;
using System.Collections.Generic;
using CoreECS.Defines;

namespace CoreECS.Managers
{
    /// <summary>
    /// Structure-of-arrays storage for one archetype chunk: entity ids, dense component columns, and sparse proxies per row.
    /// </summary>
    internal sealed class ComponentChunk
    {
        internal const int DefaultChunkCapacity = 64;

        private readonly DenseColumn[] m_denseColumns;
        private readonly Dictionary<(Type type, int instanceIndex), int> m_columnIndexByTypeInstance;

        /// <summary>
        /// Maximum number of rows this chunk can hold.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Number of live rows in this chunk.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Entity id per row.
        /// </summary>
        public ulong[] EntityIds { get; }

        /// <summary>
        /// Sparse component handle lists per row.
        /// </summary>
        public SparseSetProxy[] Proxies { get; }

        /// <summary>
        /// Creates a chunk with columns derived from the archetype signature.
        /// </summary>
        /// <param name="signature">Dense component layout for this archetype.</param>
        /// <param name="capacity">Row capacity; defaults to <see cref="DefaultChunkCapacity"/>.</param>
        internal ComponentChunk(ArchetypeSignature signature, int capacity = DefaultChunkCapacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Chunk capacity must be at least 1.");

            Capacity = capacity;
            EntityIds = new ulong[capacity];
            Proxies = new SparseSetProxy[capacity];
            m_denseColumns = DenseColumn.CreateColumns(signature, capacity, out m_columnIndexByTypeInstance);
        }

        /// <summary>
        /// Appends a row for <paramref name="entityId"/> and returns its index.
        /// </summary>
        /// <param name="entityId">Entity occupying the new row.</param>
        /// <returns>Row index of the added entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the chunk is full.</exception>
        public int AddRow(ulong entityId)
        {
            if (Count >= Capacity)
                throw new InvalidOperationException("Chunk is full.");

            var row = Count;
            EntityIds[row] = entityId;
            Proxies[row] = new SparseSetProxy();
            for (var i = 0; i < m_denseColumns.Length; i++)
                m_denseColumns[i].InitRow(row);
            Count++;
            return row;
        }

        /// <summary>
        /// Removes a row using swap-with-last. Caller must relocate surviving component refs.
        /// </summary>
        /// <param name="row">Row index to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="row"/> is out of range.</exception>
        public void RemoveRowSwapBack(int row)
        {
            if (row < 0 || row >= Count)
                throw new ArgumentOutOfRangeException(nameof(row));

            var last = Count - 1;
            if (row != last)
            {
                EntityIds[row] = EntityIds[last];
                Proxies[row] = Proxies[last];
                for (var i = 0; i < m_denseColumns.Length; i++)
                    m_denseColumns[i].SwapRow(row, last);
            }

            Proxies[last] = null;
            EntityIds[last] = 0;
            for (var i = 0; i < m_denseColumns.Length; i++)
                m_denseColumns[i].ClearRow(last);
            Count--;
        }

        /// <summary>
        /// Gets a reference to a dense component value at <paramref name="row"/> for type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Dense component type.</typeparam>
        /// <param name="row">Row index.</param>
        /// <param name="instanceIndex">Zero-based instance index within the type (0..Count-1 in signature).</param>
        /// <returns>Reference to the stored component value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when row or instance index is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the type/instance is not in this chunk's signature.</exception>
        internal ref T GetDense<T>(int row, int instanceIndex = 0) where T : struct
        {
            if (row < 0 || row >= Count)
                throw new ArgumentOutOfRangeException(nameof(row));

            var column = (DenseColumn<T>)GetColumn(typeof(T), instanceIndex);
            return ref column.GetRef(row);
        }

        private DenseColumn GetColumn(Type type, int instanceIndex)
        {
            if (!m_columnIndexByTypeInstance.TryGetValue((type, instanceIndex), out var index))
                throw new InvalidOperationException($"Archetype chunk has no dense column for {type.Name} instance {instanceIndex}.");

            return m_denseColumns[index];
        }

        private abstract class DenseColumn
        {
            public abstract void InitRow(int row);
            public abstract void SwapRow(int row, int other);
            public abstract void ClearRow(int row);

            public static DenseColumn[] CreateColumns(
                ArchetypeSignature signature,
                int capacity,
                out Dictionary<(Type type, int instanceIndex), int> columnIndexByTypeInstance)
            {
                columnIndexByTypeInstance = new Dictionary<(Type, int), int>();
                var entries = signature.Entries;
                if (entries.Count == 0)
                    return Array.Empty<DenseColumn>();

                var columns = new DenseColumn[CountColumns(entries)];
                var columnIndex = 0;
                for (var e = 0; e < entries.Count; e++)
                {
                    var entry = entries[e];
                    for (var instance = 0; instance < entry.Count; instance++)
                    {
                        columnIndexByTypeInstance[(entry.Type, instance)] = columnIndex;
                        columns[columnIndex] = Create(entry.Type, instance, capacity);
                        columnIndex++;
                    }
                }

                return columns;
            }

            private static int CountColumns(IReadOnlyList<ArchetypeEntry> entries)
            {
                var total = 0;
                for (var i = 0; i < entries.Count; i++)
                    total += entries[i].Count;
                return total;
            }

            private static DenseColumn Create(Type type, int instanceIndex, int capacity)
            {
                var columnType = typeof(DenseColumn<>).MakeGenericType(type);
                return (DenseColumn)Activator.CreateInstance(columnType, instanceIndex, capacity);
            }
        }

        private sealed class DenseColumn<T> : DenseColumn where T : struct
        {
            private readonly T[] m_values;
            private readonly ComponentRefCore[] m_refCores;

            public DenseColumn(int instanceIndex, int capacity)
            {
                m_values = new T[capacity];
                m_refCores = new ComponentRefCore[capacity];
            }

            public ref T GetRef(int row) => ref m_values[row];

            public override void InitRow(int row)
            {
                m_values[row] = default;
                m_refCores[row] = null;
            }

            public override void SwapRow(int row, int other)
            {
                var tempValue = m_values[row];
                m_values[row] = m_values[other];
                m_values[other] = tempValue;

                var tempCore = m_refCores[row];
                m_refCores[row] = m_refCores[other];
                m_refCores[other] = tempCore;
            }

            public override void ClearRow(int row)
            {
                m_values[row] = default;
                m_refCores[row] = null;
            }
        }
    }
}
