using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CoreECS.Defines;
using CoreECS.Utils;

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
        private readonly Action<IComponentRefCore, ulong> m_revisionChanged;

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
        /// <param name="revisionChanged">Callback invoked when a dense component revision changes.</param>
        internal ComponentChunk(ArchetypeSignature signature, int capacity = DefaultChunkCapacity,
            Action<IComponentRefCore, ulong> revisionChanged = null)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Chunk capacity must be at least 1.");

            Capacity = capacity;
            m_revisionChanged = revisionChanged;
            EntityIds = new ulong[capacity];
            Proxies = new SparseSetProxy[capacity];
            m_denseColumns = DenseColumn.CreateColumns(signature, capacity, this, revisionChanged, out m_columnIndexByTypeInstance);
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
        /// Removes a row using swap-with-last and relocates surviving dense component refs.
        /// </summary>
        /// <param name="row">Row index to remove.</param>
        /// <returns>The entity id that was swapped into <paramref name="row"/>, or 0 if the removed row was the last row.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="row"/> is out of range.</exception>
        public ulong RemoveRowSwapBack(int row)
        {
            if (row < 0 || row >= Count)
                throw new ArgumentOutOfRangeException(nameof(row));

            var last = Count - 1;
            ulong moved = 0;
            if (row != last)
            {
                EntityIds[row] = EntityIds[last];
                Proxies[row] = Proxies[last];
                for (var i = 0; i < m_denseColumns.Length; i++)
                    m_denseColumns[i].SwapRow(row, last);
                moved = EntityIds[row];
            }

            Proxies[last] = null;
            EntityIds[last] = 0;
            for (var i = 0; i < m_denseColumns.Length; i++)
                m_denseColumns[i].ClearRow(last);
            Count--;
            return moved;
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
        internal ref T GetDense<T>(int row, int instanceIndex = 0) where T : struct, IComponent<T>
        {
            if (row < 0 || row >= Count)
                throw new ArgumentOutOfRangeException(nameof(row));

            var column = (DenseColumn<T>)GetColumn(typeof(T), instanceIndex);
            return ref column.GetRef(row);
        }

        /// <summary>
        /// Allocates a fresh dense component instance at (<typeparamref name="T"/>, <paramref name="instanceIndex"/>) for <paramref name="row"/>.
        /// Runs the component's OnCreate hook and returns the backing reference core.
        /// </summary>
        internal ComponentRefCore CreateDense<T>(int row, int instanceIndex, ulong entityId, bool hasInitial, T initial)
            where T : struct, IComponent<T>
        {
            var column = (DenseColumn<T>)GetColumn(typeof(T), instanceIndex);
            return column.Allocate(row, entityId, hasInitial, initial);
        }

        /// <summary>
        /// Copies shared dense columns and moves sparse proxy handles from this chunk's <paramref name="sourceRow"/>
        /// into <paramref name="dest"/>'s <paramref name="destRow"/>, adopting surviving dense reference cores.
        /// </summary>
        /// <param name="dest">Destination chunk.</param>
        /// <param name="sourceRow">Row index in this chunk.</param>
        /// <param name="destRow">Row index in the destination chunk.</param>
        /// <param name="excludeType">Optional component type whose instance <paramref name="excludeInstance"/> is skipped; instances after it shift down by one.</param>
        /// <param name="excludeInstance">Instance index to skip when <paramref name="excludeType"/> is set.</param>
        internal void MigrateRowInto(ComponentChunk dest, int sourceRow, int destRow, Type excludeType, int excludeInstance)
        {
            foreach (var kv in m_columnIndexByTypeInstance)
            {
                var type = kv.Key.type;
                var instance = kv.Key.instanceIndex;
                var destInstance = instance;

                if (excludeType != null && type == excludeType)
                {
                    if (instance == excludeInstance) continue;
                    if (instance > excludeInstance) destInstance = instance - 1;
                }

                if (dest.m_columnIndexByTypeInstance.TryGetValue((type, destInstance), out var di))
                    dest.m_denseColumns[di].AdoptFrom(m_denseColumns[kv.Value], sourceRow, destRow);
            }

            var srcProxy = Proxies[sourceRow];
            var destProxy = dest.Proxies[destRow];
            if (srcProxy != null && destProxy != null && srcProxy.Handles.Count > 0)
            {
                for (var i = 0; i < srcProxy.Handles.Count; i++)
                    destProxy.Add(srcProxy.Handles[i]);
                srcProxy.Clear();
            }
        }

        /// <summary>
        /// Finds the dense (type, instance) slot occupied by <paramref name="core"/> at <paramref name="row"/>.
        /// </summary>
        internal bool TryGetDenseSlot(int row, IComponentRefCore core, out Type type, out int instanceIndex)
        {
            foreach (var kv in m_columnIndexByTypeInstance)
            {
                if (ReferenceEquals(m_denseColumns[kv.Value].GetCoreAt(row), core))
                {
                    type = kv.Key.type;
                    instanceIndex = kv.Key.instanceIndex;
                    return true;
                }
            }

            type = null;
            instanceIndex = -1;
            return false;
        }

        /// <summary>
        /// Destroys a single dense instance (runs OnDestroy and releases the reference core).
        /// </summary>
        internal void DestroyDenseSlot(int row, Type type, int instanceIndex)
        {
            GetColumn(type, instanceIndex).DestroyAt(row);
        }

        /// <summary>
        /// Destroys every dense instance stored at <paramref name="row"/> (runs OnDestroy and releases reference cores).
        /// </summary>
        internal void DestroyDenseRow(int row)
        {
            for (var i = 0; i < m_denseColumns.Length; i++)
                m_denseColumns[i].DestroyAt(row);
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
            public abstract IComponentRefCore GetCoreAt(int row);
            public abstract void AdoptFrom(DenseColumn source, int sourceRow, int destRow);
            public abstract void DestroyAt(int row);

            public static DenseColumn[] CreateColumns(
                ArchetypeSignature signature,
                int capacity,
                ComponentChunk chunk,
                Action<IComponentRefCore, ulong> revisionChanged,
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
                        columns[columnIndex] = Create(entry.Type, instance, capacity, chunk, revisionChanged);
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

            private static DenseColumn Create(Type type, int instanceIndex, int capacity, ComponentChunk chunk,
                Action<IComponentRefCore, ulong> revisionChanged)
            {
                var columnType = typeof(DenseColumn<>).MakeGenericType(type);
                return (DenseColumn)Activator.CreateInstance(columnType, instanceIndex, capacity, chunk, revisionChanged);
            }
        }

        private sealed class DenseColumn<T> : DenseColumn, IComponentRefLocator where T : struct, IComponent<T>
        {
            private readonly ComponentChunk m_chunk;
            private readonly Action<IComponentRefCore, ulong> m_revisionChanged;
            private readonly T[] m_values;
            private readonly ComponentRefCore[] m_refCores;
            private readonly uint[] m_versions;
            private readonly uint[] m_revisions;

            public DenseColumn(int instanceIndex, int capacity, ComponentChunk chunk,
                Action<IComponentRefCore, ulong> revisionChanged)
            {
                m_chunk = chunk;
                m_revisionChanged = revisionChanged;
                m_values = new T[capacity];
                m_refCores = new ComponentRefCore[capacity];
                m_versions = new uint[capacity];
                m_revisions = new uint[capacity];
            }

            public ref T GetRef(int row) => ref m_values[row];

            public ComponentRefCore Allocate(int row, ulong entityId, bool hasInitial, T initial)
            {
                m_versions[row] = (m_versions[row] % uint.MaxValue) + 1;
                m_revisions[row] = 0;
                m_values[row] = hasInitial ? initial : default;

                var core = ComponentRefCore.Pool.Get();
                core.Allocate(this, row, m_versions[row]);
                m_refCores[row] = core;

                try
                {
                    m_values[row].OnCreate(entityId);
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }

                return core;
            }

            public override void InitRow(int row)
            {
                m_values[row] = default;
                m_refCores[row] = null;
                m_revisions[row] = 0;
            }

            public override void SwapRow(int row, int other)
            {
                var tempValue = m_values[row];
                m_values[row] = m_values[other];
                m_values[other] = tempValue;

                var tempCore = m_refCores[row];
                m_refCores[row] = m_refCores[other];
                m_refCores[other] = tempCore;

                var tempVersion = m_versions[row];
                m_versions[row] = m_versions[other];
                m_versions[other] = tempVersion;

                var tempRevision = m_revisions[row];
                m_revisions[row] = m_revisions[other];
                m_revisions[other] = tempRevision;

                m_refCores[row]?.Relocate(row);
            }

            public override void ClearRow(int row)
            {
                m_values[row] = default;
                m_refCores[row] = null;
                m_versions[row] = (m_versions[row] % uint.MaxValue) + 1;
                m_revisions[row] = 0;
            }

            public override IComponentRefCore GetCoreAt(int row) => m_refCores[row];

            public override void AdoptFrom(DenseColumn source, int sourceRow, int destRow)
            {
                var src = (DenseColumn<T>)source;
                m_values[destRow] = src.m_values[sourceRow];
                m_revisions[destRow] = src.m_revisions[sourceRow];

                var core = src.m_refCores[sourceRow];
                m_refCores[destRow] = core;
                if (core != null)
                {
                    m_versions[destRow] = core.Version;
                    core.Allocate(this, destRow, core.Version);
                }
                else
                {
                    m_versions[destRow] = (m_versions[destRow] % uint.MaxValue) + 1;
                }
            }

            public override void DestroyAt(int row)
            {
                var core = m_refCores[row];
                if (core == null) return;

                var entityId = m_chunk.EntityIds[row];
                try
                {
                    m_values[row].OnDestroy(entityId);
                }
                catch (Exception e)
                {
                    Log.Exp(e);
                }

                m_refCores[row] = null;
                m_values[row] = default;
                m_versions[row] = (m_versions[row] % uint.MaxValue) + 1;
                m_revisions[row] = 0;
                ComponentRefCore.Pool.Release(core);
            }

            public bool NotNull(uint version, int offset)
            {
                if (offset < 0 || offset >= m_chunk.Count) return false;
                if (m_chunk.EntityIds[offset] == 0) return false;
                return m_versions[offset] == version && m_refCores[offset] != null;
            }

            public ref TAny Get<TAny>(int offset) where TAny : struct, IComponent<TAny>
            {
#if NET6_0_OR_GREATER
                return ref Unsafe.As<T, TAny>(ref m_values[offset]);
#else
                unsafe
                {
                    fixed (T* valuePtr = &m_values[offset])
                    {
                        TAny* tPtr = (TAny*)valuePtr;
                        return ref *tPtr;
                    }
                }
#endif
            }

            public bool IsT(Type type) => type == typeof(T);

            public Type GetT() => typeof(T);

            public ulong GetEntityId(int offset)
            {
                if (offset < 0 || offset >= m_chunk.Count) return 0;
                return m_chunk.EntityIds[offset];
            }

            public IComponentRefCore GetRefCore(int offset)
            {
                if (offset < 0 || offset >= m_chunk.Count) return null;
                return m_refCores[offset];
            }

            public uint GetRevision(int offset)
            {
                if (offset < 0 || offset >= m_chunk.Count) return 0;
                return m_revisions[offset];
            }

            public uint ChangeRevision(int offset)
            {
                if (offset < 0 || offset >= m_chunk.Count) return 0;
                if (m_refCores[offset] == null) return 0;

                m_revisions[offset] = (m_revisions[offset] % uint.MaxValue) + 1;
                m_revisionChanged?.Invoke(m_refCores[offset], m_chunk.EntityIds[offset]);
                return m_revisions[offset];
            }
        }
    }
}
