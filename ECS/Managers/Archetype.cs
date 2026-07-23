using System;

namespace CoreECS.Managers
{
    internal sealed class Archetype
    {
        public int Id { get; }
        public ArchetypeSignature Signature { get; }
        public int ReadLockCount { get; private set; }
        public bool IsReadLocked => ReadLockCount > 0;

        internal Archetype(int id, ArchetypeSignature signature)
        {
            Id = id;
            Signature = signature;
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
