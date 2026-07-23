using System;
using System.Collections.Generic;
using CoreECS.Defines;

namespace CoreECS.Test
{
    [TestFixture]
    public class EntityQueryTestUnit
    {
        [Test]
        public void EntityQuery_YieldsMatchingEntities()
        {
            var world = new World();
            world.Startup();
            try
            {
                var a = world.CreateEntity();
                a.CreateComponent<DenseProbe>();
                _ = world.CreateEntity();

                var matcher = EntityMatcher.With.OfAll<DenseProbe>();
                var ids = new List<ulong>();
                foreach (var entity in world.Query(matcher))
                    ids.Add(entity.EntityId);

                Assert.AreEqual(1, ids.Count);
                CollectionAssert.AreEqual(new[] { a.EntityId }, ids);
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void EntityQuery_DenseCreateWhileEnumerating_Throws()
        {
            var world = new World();
            world.Startup();
            try
            {
                var entity = world.CreateEntity();
                entity.CreateComponent<DenseProbe>();
                var matcher = EntityMatcher.With.OfAll<DenseProbe>();

                Assert.Throws<InvalidOperationException>(() =>
                {
                    foreach (var hit in world.Query(matcher))
                        hit.CreateComponent<DenseProbe>();
                });
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void EntityQuery_CreateEntityWhileEnumerating_ThrowsThenSucceedsAfterDispose()
        {
            var world = new World();
            world.Startup();
            try
            {
                // Seed one proxy-only entity so the empty archetype is a query candidate and gets read-locked.
                world.CreateEntity();
                var matcher = EntityMatcher.With;

                var enumerator = world.Query(matcher).GetEnumerator();
                Assert.IsTrue(enumerator.MoveNext());

                // CreateEntity places the new entity into the read-locked empty archetype.
                Assert.Throws<InvalidOperationException>(() => world.CreateEntity());

                enumerator.Dispose();
                Assert.DoesNotThrow(() => world.CreateEntity());
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void EntityQuery_DestroyProxyOnlyEntityWhileEnumerating_ThrowsThenSucceedsAfterDispose()
        {
            var world = new World();
            world.Startup();
            try
            {
                world.CreateEntity();
                var victim = world.CreateEntity();
                var matcher = EntityMatcher.With;

                var enumerator = world.Query(matcher).GetEnumerator();
                Assert.IsTrue(enumerator.MoveNext());

                // DestroyEntity would swap-remove a row in the read-locked empty archetype.
                Assert.Throws<InvalidOperationException>(() => world.DestroyEntity(victim.EntityId));

                enumerator.Dispose();
                Assert.DoesNotThrow(() => world.DestroyEntity(victim.EntityId));
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void EntityQuery_EarlyBreak_DisposesReadLocks()
        {
            var world = new World();
            world.Startup();
            try
            {
                Entity first = default;
                var entity = world.CreateEntity();
                entity.CreateComponent<DenseProbe>();
                var matcher = EntityMatcher.With.OfAll<DenseProbe>();

                foreach (var hit in world.Query(matcher))
                {
                    first = hit;
                    break;
                }

                Assert.DoesNotThrow(() => first.CreateComponent<DenseAux>());
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void EntityQuery_SparseOnlyMatcher_ScansEmptyArchetypeAndProxyFilters()
        {
            var world = new World();
            world.Startup();
            try
            {
                var sparseOnly = world.CreateEntity();
                sparseOnly.CreateComponent<SparseProbe>();
                var noSparse = world.CreateEntity();

                var ids = new List<ulong>();
                foreach (var entity in world.Query(EntityMatcher.With.OfAll<SparseProbe>()))
                    ids.Add(entity.EntityId);

                CollectionAssert.AreEqual(new[] { sparseOnly.EntityId }, ids);
                CollectionAssert.DoesNotContain(ids, noSparse.EntityId);
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void EntityQuery_Dispose_ReleasesReadLocks()
        {
            var world = new World();
            world.Startup();
            try
            {
                var entity = world.CreateEntity();
                entity.CreateComponent<DenseProbe>();
                var query = world.Query(EntityMatcher.With.OfAll<DenseProbe>());
                var enumerator = query.GetEnumerator();

                Assert.IsTrue(enumerator.MoveNext());
                enumerator.Dispose();

                Assert.DoesNotThrow(() => entity.CreateComponent<DenseAux>());
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void CommandBuffer_DeferDenseCreate_DuringQuery_ThenPlayback()
        {
            var world = new World();
            world.Startup();
            try
            {
                var e = world.CreateEntity();
                e.CreateComponent<DenseProbe>();
                var matcher = EntityMatcher.With.OfAll<DenseProbe>();

                using (var buf = world.RentCommandBuffer(CommandBufferFlag.MustManualPlaybackOnDispose))
                {
                    foreach (var hit in world.Query(matcher))
                        buf.CreateComponentDefer<DenseProbe>(hit);

                    buf.Playback();
                }

                Assert.That(e.GetComponentCount<DenseProbe>(), Is.EqualTo(2));
            }
            finally
            {
                world.Shutdown();
            }
        }
    }
}
