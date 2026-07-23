using System;
using System.Collections.Generic;

namespace CoreECS.Managers
{
    public readonly struct ArchetypeEntry
    {
        public readonly Type Type;
        public readonly int Count;

        public ArchetypeEntry(Type type, int count)
        {
            Type = type;
            Count = count;
        }
    }

    public readonly struct ArchetypeSignature : IEquatable<ArchetypeSignature>
    {
        public static ArchetypeSignature Empty { get; } = new ArchetypeSignature(Array.Empty<ArchetypeEntry>());

        public IReadOnlyList<ArchetypeEntry> Entries { get; }

        private ArchetypeSignature(ArchetypeEntry[] entries)
        {
            Entries = entries;
        }

        public static ArchetypeSignature From(params ArchetypeEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
                return Empty;

            var sorted = new ArchetypeEntry[entries.Length];
            Array.Copy(entries, sorted, entries.Length);
            Array.Sort(sorted, (a, b) =>
            {
                var nameCompare = string.CompareOrdinal(a.Type.FullName, b.Type.FullName);
                return nameCompare != 0 ? nameCompare : a.Count.CompareTo(b.Count);
            });

            for (var i = 0; i < sorted.Length; i++)
            {
                if (sorted[i].Count < 1)
                    throw new ArgumentOutOfRangeException(nameof(entries), "Archetype entry count must be at least 1.");
            }

            return new ArchetypeSignature(sorted);
        }

        public bool Equals(ArchetypeSignature other)
        {
            var left = Entries;
            var right = other.Entries;
            if (left.Count != right.Count)
                return false;

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].Type != right[i].Type || left[i].Count != right[i].Count)
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => obj is ArchetypeSignature other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                for (var i = 0; i < Entries.Count; i++)
                {
                    hash = (hash * 397) ^ (Entries[i].Type?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ Entries[i].Count;
                }

                return hash;
            }
        }

        public static bool operator ==(ArchetypeSignature left, ArchetypeSignature right) => left.Equals(right);

        public static bool operator !=(ArchetypeSignature left, ArchetypeSignature right) => !left.Equals(right);

        /// <summary>
        /// Returns true when the signature contains at least one instance of <paramref name="componentType"/>.
        /// </summary>
        public bool Has(Type componentType)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry.Type == componentType && entry.Count >= 1)
                    return true;
            }

            return false;
        }
    }
}
