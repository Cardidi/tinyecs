namespace TinyECS.Defines
{
    public interface IWorldElement : IWorldElement<IWorld> {}

    public interface IWorldElement<out TWorld> where TWorld : IWorld
    {
        public TWorld World { get; }
    }
}