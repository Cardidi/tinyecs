namespace CoreECS.Defines
{
    /// <summary>
    /// Marker for components stored in the sparse ComponentStore path.
    /// </summary>
    public interface ISparseComponent<TComponent> : IComponent<TComponent>
        where TComponent : struct, ISparseComponent<TComponent>
    {
    }
}
