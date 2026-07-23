using System;
using CoreECS.Defines;

namespace CoreECS.Test
{
    [TestFixture]
    public class CommandBufferTestUnit
    {
        [Test]
        public void CommandBuffer_Default_AutoPlaybackOnDispose()
        {
            var world = new World();
            world.Startup();
            try
            {
                var e = world.CreateEntity();

                using (var buf = world.RentCommandBuffer())
                    buf.CreateComponentDefer<DenseProbe>(e);

                Assert.That(e.HasComponent<DenseProbe>(), Is.True);
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void CommandBuffer_MustManual_ThrowsIfDisposeWithoutPlayback()
        {
            var world = new World();
            world.Startup();
            try
            {
                var e = world.CreateEntity();
                var buf = world.RentCommandBuffer(CommandBufferFlag.MustManualPlaybackOnDispose);
                buf.CreateComponentDefer<DenseProbe>(e);

                Assert.That(() => buf.Dispose(), Throws.InvalidOperationException);
            }
            finally
            {
                world.Shutdown();
            }
        }

        [Test]
        public void CommandBuffer_Discard_DropsPending()
        {
            var world = new World();
            world.Startup();
            try
            {
                var e = world.CreateEntity();

                using (var buf = world.RentCommandBuffer(CommandBufferFlag.DiscardPendingOnDispose))
                    buf.CreateComponentDefer<DenseProbe>(e);

                Assert.That(e.HasComponent<DenseProbe>(), Is.False);
            }
            finally
            {
                world.Shutdown();
            }
        }
    }
}
