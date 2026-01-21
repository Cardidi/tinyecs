namespace TinyECS.Defines
{
    public interface IWorldManager : IWorldElement
    {
        public void OnManagerCreated();
        
        public void OnWorldStarted();

        public void OnWorldEnded();

        public void OnManagerDestroyed();
    }
}