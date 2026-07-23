using System;
using CoreECS.Managers;
using NUnit.Framework;

namespace CoreECS.Test
{
    public struct DenseAux : Defines.IComponent<DenseAux> { }

    public struct DenseValue : Defines.IComponent<DenseValue>
    {
        public int V;
    }

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

        [Test]
        public void DenseCreate_MigratesArchetypeByCount()
        {
            var world = new World();
            world.Startup();
            try
            {
                var e = world.CreateEntity();
                e.CreateComponent<DenseProbe>();
                e.CreateComponent<DenseProbe>();
                Assert.AreEqual(2, e.GetComponentCount<DenseProbe>());
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void DenseDestroy_ReducesCountAndRemovesHasWhenLast()
        {
            var world = new World();
            world.Startup();
            try
            {
                var e = world.CreateEntity();
                e.CreateComponent<DenseProbe>();
                e.CreateComponent<DenseProbe>();
                Assert.AreEqual(2, e.GetComponentCount<DenseProbe>());

                var refs = e.GetComponents<DenseProbe>();
                e.DestroyComponent(refs[0]);
                Assert.AreEqual(1, e.GetComponentCount<DenseProbe>());
                Assert.IsTrue(e.HasComponent<DenseProbe>());

                var remaining = e.GetComponents<DenseProbe>();
                e.DestroyComponent(remaining[0]);
                Assert.AreEqual(0, e.GetComponentCount<DenseProbe>());
                Assert.IsFalse(e.HasComponent<DenseProbe>());
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void DenseCreate_ThrowsWhenSourceArchetypeReadLocked()
        {
            var world = new World();
            world.Startup();
            try
            {
                var cm = world.GetManager<ComponentManager>();
                var e = world.CreateEntity();
                e.CreateComponent<DenseProbe>();

                var source = cm.GetEntityArchetype(e.EntityId);
                source.AddReadLock();

                Assert.Throws<InvalidOperationException>(() => e.CreateComponent<DenseAux>());
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void DenseCreate_ThrowsWhenDestinationArchetypeReadLocked()
        {
            var world = new World();
            world.Startup();
            try
            {
                var cm = world.GetManager<ComponentManager>();

                // Materialize the destination archetype {DenseProbe:1} via a first entity.
                var seed = world.CreateEntity();
                seed.CreateComponent<DenseProbe>();
                var dest = cm.GetEntityArchetype(seed.EntityId);
                dest.AddReadLock();

                // Second entity starts in the (unlocked) empty archetype; its create targets the locked dest.
                var e = world.CreateEntity();
                Assert.IsFalse(cm.GetEntityArchetype(e.EntityId).IsReadLocked);
                Assert.Throws<InvalidOperationException>(() => e.CreateComponent<DenseProbe>());
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void DenseGetComponentRo_StillWorksAfterMigrate()
        {
            var world = new World();
            world.Startup();
            try
            {
                var e = world.CreateEntity();
                e.CreateComponent<DenseValue>(new DenseValue { V = 7 });

                // Adding another dense component migrates the entity to a new archetype,
                // relocating the surviving DenseValue ref into the destination chunk.
                e.CreateComponent<DenseAux>();

                var got = e.GetComponent<DenseValue>();
                Assert.IsTrue(got.NotNull);
                Assert.AreEqual(7, got.RO.V);
            }
            finally
            {
                world.Shutdown();
            }
        }
    }
}
