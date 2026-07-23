using System;
using CoreECS.Managers;
using NUnit.Framework;

namespace CoreECS.Test
{
    public struct DenseAux : Defines.IComponent<DenseAux> { }

    public class ArchetypeChunkTestUnit
    {
        [Test]
        public void ArchetypeSignature_From_RejectsCountLessThanOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ArchetypeSignature.From(new ArchetypeEntry(typeof(DenseProbe), 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ArchetypeSignature.From(new ArchetypeEntry(typeof(DenseProbe), -1)));
        }

        [Test]
        public void ArchetypeSignature_Equals_ByTypeAndCount()
        {
            var a = ArchetypeSignature.From(
                new ArchetypeEntry(typeof(DenseProbe), 1),
                new ArchetypeEntry(typeof(DenseAux), 2));
            var b = ArchetypeSignature.From(
                new ArchetypeEntry(typeof(DenseProbe), 1),
                new ArchetypeEntry(typeof(DenseAux), 2));
            var c = ArchetypeSignature.From(new ArchetypeEntry(typeof(DenseProbe), 2));
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void ArchetypeRegistry_Empty_IsProxyOnly()
        {
            var registry = new ArchetypeRegistry();
            Assert.AreEqual(0, registry.Empty.Signature.Entries.Count);
            Assert.AreSame(registry.Empty, registry.GetOrCreate(ArchetypeSignature.Empty));
        }

        [Test]
        public void ComponentChunk_AddAndRemoveRow_UpdatesCountAndProxy()
        {
            var registry = new ArchetypeRegistry();
            var sig = ArchetypeSignature.From(new ArchetypeEntry(typeof(DenseProbe), 1));
            var arch = registry.GetOrCreate(sig);
            var chunk = arch.GetChunkWithSpace();
            var row = chunk.AddRow(42UL);
            Assert.AreEqual(42UL, chunk.EntityIds[row]);
            Assert.IsNotNull(chunk.Proxies[row]);
            Assert.AreEqual(0, chunk.Proxies[row].Handles.Count);
            chunk.RemoveRowSwapBack(row);
            Assert.AreEqual(0, chunk.Count);
        }
    }
}
