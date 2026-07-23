using System;

namespace CoreECS.Defines
{
    /// <summary>
    /// Records structural component commands so they can be played after a locked query finishes.
    /// </summary>
    public interface ICommandBuffer : IDisposable
    {
        /// <summary>
        /// Defers creating a component with the default value on <paramref name="entity"/>.
        /// </summary>
        /// <typeparam name="T">Component type to create.</typeparam>
        /// <param name="entity">Entity that will receive the component.</param>
        /// <exception cref="InvalidOperationException">Thrown when the buffer is disposed.</exception>
        void CreateComponentDefer<T>(Entity entity) where T : struct, IComponent<T>;

        /// <summary>
        /// Defers creating a component with an initial value on <paramref name="entity"/>.
        /// </summary>
        /// <typeparam name="T">Component type to create.</typeparam>
        /// <param name="entity">Entity that will receive the component.</param>
        /// <param name="initial">Initial component value.</param>
        /// <exception cref="InvalidOperationException">Thrown when the buffer is disposed.</exception>
        void CreateComponentDefer<T>(Entity entity, T initial) where T : struct, IComponent<T>;

        /// <summary>
        /// Defers destroying one component of type <typeparamref name="T"/> from <paramref name="entity"/>.
        /// </summary>
        /// <typeparam name="T">Component type to destroy.</typeparam>
        /// <param name="entity">Entity that owns the component.</param>
        /// <exception cref="InvalidOperationException">Thrown when the buffer is disposed.</exception>
        void DestroyComponentDefer<T>(Entity entity) where T : struct, IComponent<T>;

        /// <summary>
        /// Defers destroying the specified typeless component from <paramref name="entity"/>.
        /// </summary>
        /// <param name="entity">Entity that owns the component.</param>
        /// <param name="component">Component reference to destroy.</param>
        /// <exception cref="InvalidOperationException">Thrown when the buffer is disposed.</exception>
        void DestroyComponentDefer(Entity entity, ComponentRef component);

        /// <summary>
        /// Plays all pending commands in recording order.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the buffer is disposed.</exception>
        void Playback();
    }
}
