namespace TinyECS.Defines
{
    public interface IWorldManager : IWorldElement
    {
        public void OnManagerCreated(IWorld world);
        
        public void OnWorldStarted(IWorld world);

        public void OnWorldEnded(IWorld world);

        public void OnManagerDestroyed(IWorld world);
    }
}