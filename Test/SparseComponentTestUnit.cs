using CoreECS.Defines;
using CoreECS.Managers;
using NUnit.Framework;

namespace CoreECS.Test
{
    public struct DenseProbe : IComponent<DenseProbe> { }
    public struct SparseProbe : ISparseComponent<SparseProbe> { }

    public class SparseComponentTestUnit
    {
        [Test]
        public void ComponentStorageKind_DetectsSparseInterface()
        {
            Assert.IsFalse(ComponentStorageKind.IsSparse<DenseProbe>());
            Assert.IsTrue(ComponentStorageKind.IsSparse<SparseProbe>());
            Assert.IsTrue(ComponentStorageKind.IsSparse(typeof(SparseProbe)));
        }

        [Test]
        public void SparseCreate_DoesNotRequireDenseColumns()
        {
            var world = new World();
            world.Startup();
            try
            {
                var e = world.CreateEntity();
                e.CreateComponent<SparseProbe>();
                Assert.IsTrue(e.HasComponent<SparseProbe>());
                Assert.IsFalse(e.HasComponent<DenseProbe>());
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void SparseCreate_DoesNotChangeArchetypeId()
        {
            var world = new World();
            world.Startup();
            try
            {
                var cm = world.GetManager<ComponentManager>();
                var e = world.CreateEntity();

                var before = cm.GetEntityArchetype(e.EntityId).Id;
                e.CreateComponent<SparseProbe>();
                var after = cm.GetEntityArchetype(e.EntityId).Id;

                Assert.AreEqual(before, after);
            }
            finally
            {
                world.Shutdown();
            }
        }
    }
}
