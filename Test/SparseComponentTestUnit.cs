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
    }
}
