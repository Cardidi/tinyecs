namespace TinyECS.Defines
{
    /// <summary>
    /// The minimal define of a system to process entities. It can only driven by world.
    /// </summary>
    public interface ISystem
    {
        public ulong TickGroup { get; }
        
        public void OnCreate(IWorld world);
        
        public void OnTick(IWorld world);

        public void OnDestroy(IWorld world);
    }
}