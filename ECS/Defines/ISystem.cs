namespace TinyECS.Defines
{
    /// <summary>
    /// The minimal define of a system to process entities. It can only driven by world.
    /// </summary>
    public interface ISystem : IWorldElement
    {
        public ulong TickGroup { get; }
        
        public void OnCreate();
        
        public void OnTick();

        public void OnDestroy();
    }
}