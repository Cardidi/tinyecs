namespace CoreECS.Defines
{
    /// <summary>
    /// Controls how a rented command buffer handles pending commands when it is disposed.
    /// </summary>
    public enum CommandBufferFlag
    {
        /// <summary>
        /// Uses the default dispose behavior, currently <see cref="AutoPlaybackOnDispose"/>.
        /// </summary>
        Default = AutoPlaybackOnDispose,

        /// <summary>
        /// Plays pending commands automatically when the buffer is disposed.
        /// </summary>
        AutoPlaybackOnDispose = 0,

        /// <summary>
        /// Drops pending commands when the buffer is disposed.
        /// </summary>
        DiscardPendingOnDispose = 1,

        /// <summary>
        /// Requires <see cref="ICommandBuffer.Playback"/> before dispose when commands are pending.
        /// </summary>
        MustManualPlaybackOnDispose = 2,
    }
}
