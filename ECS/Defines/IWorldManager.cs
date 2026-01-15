namespace TinyECS.Defines
{
    public interface IWorldManager
    {
        public IWorld World { get; }
        
        public void OnManagerCreated(IWorld world);
        
        public void OnWorldStarted(IWorld world);

        public void OnWorldEnded(IWorld world);

        public void OnManagerDestroyed(IWorld world);
    }
}