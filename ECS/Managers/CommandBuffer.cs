using System;
using System.Collections.Generic;
using CoreECS.Defines;
using CoreECS.Utils;

namespace CoreECS.Managers
{
    /// <summary>
    /// Pooled implementation of <see cref="ICommandBuffer"/> that replays deferred component operations.
    /// </summary>
    public sealed class CommandBuffer : ICommandBuffer
    {
        private delegate void CommandHandler(Command command);

        private enum CommandKind
        {
            CreateComponent,
            CreateComponentWithInitial,
            DestroyComponent,
            DestroyComponentRef,
        }

        private struct Command
        {
            public CommandKind Kind;
            public Entity Entity;
            public ComponentRef Component;
            public object Initial;
            public CommandHandler Handler;

            public void Playback()
            {
                switch (Kind)
                {
                    case CommandKind.CreateComponent:
                    case CommandKind.CreateComponentWithInitial:
                    case CommandKind.DestroyComponent:
                    case CommandKind.DestroyComponentRef:
                        Handler(this);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported command buffer command.");
                }
            }
        }

        private static class CommandHandlers<T> where T : struct, IComponent<T>
        {
            public static readonly CommandHandler CreateComponent = command => command.Entity.CreateComponent<T>();
            public static readonly CommandHandler CreateComponentWithInitial = command => command.Entity.CreateComponent((T)command.Initial);
            public static readonly CommandHandler DestroyComponent = command => command.Entity.DestroyComponent<T>();
        }

        private static readonly CommandHandler DestroyComponentRef = command => command.Entity.DestroyComponent(command.Component);

        private static readonly Pool<CommandBuffer> Pool = new(
            createFunc: () => new CommandBuffer(),
            returnAction: commandBuffer => commandBuffer.Reset());

        private readonly List<Command> m_commands = new();
        private World m_world;
        private CommandBufferFlag m_flag;
        private bool m_disposed = true;

        private CommandBuffer() {}

        /// <summary>
        /// Rents a command buffer bound to <paramref name="world"/>.
        /// </summary>
        /// <param name="world">World whose entities will be mutated during playback.</param>
        /// <param name="flag">Dispose behavior for pending commands.</param>
        /// <returns>A rented command buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="world"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="flag"/> is not supported.</exception>
        public static CommandBuffer Rent(World world, CommandBufferFlag flag)
        {
            Assertion.ArgumentNotNull(world, nameof(world));
            ValidateFlag(flag);

            var commandBuffer = Pool.Get();
            commandBuffer.m_world = world;
            commandBuffer.m_flag = flag;
            commandBuffer.m_disposed = false;
            return commandBuffer;
        }

        /// <inheritdoc />
        public void CreateComponentDefer<T>(Entity entity) where T : struct, IComponent<T>
        {
            EnsureOpen();
            EnsureValidEntity(entity);

            m_commands.Add(new Command
            {
                Kind = CommandKind.CreateComponent,
                Entity = entity,
                Handler = CommandHandlers<T>.CreateComponent,
            });
        }

        /// <inheritdoc />
        public void CreateComponentDefer<T>(Entity entity, T initial) where T : struct, IComponent<T>
        {
            EnsureOpen();
            EnsureValidEntity(entity);

            m_commands.Add(new Command
            {
                Kind = CommandKind.CreateComponentWithInitial,
                Entity = entity,
                Initial = initial,
                Handler = CommandHandlers<T>.CreateComponentWithInitial,
            });
        }

        /// <inheritdoc />
        public void DestroyComponentDefer<T>(Entity entity) where T : struct, IComponent<T>
        {
            EnsureOpen();
            EnsureValidEntity(entity);

            m_commands.Add(new Command
            {
                Kind = CommandKind.DestroyComponent,
                Entity = entity,
                Handler = CommandHandlers<T>.DestroyComponent,
            });
        }

        /// <inheritdoc />
        public void DestroyComponentDefer(Entity entity, ComponentRef component)
        {
            EnsureOpen();
            EnsureValidEntity(entity);
            if (!component.NotNull)
                throw new InvalidOperationException("Component is not valid.");
            if (component.EntityId != entity.EntityId)
                throw new InvalidOperationException("Component does not belong to the entity.");

            m_commands.Add(new Command
            {
                Kind = CommandKind.DestroyComponentRef,
                Entity = entity,
                Component = component,
                Handler = DestroyComponentRef,
            });
        }

        /// <inheritdoc />
        public void Playback()
        {
            EnsureOpen();

            for (var i = 0; i < m_commands.Count; i++)
                m_commands[i].Playback();

            m_commands.Clear();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (m_disposed) return;

            try
            {
                if (m_flag == CommandBufferFlag.AutoPlaybackOnDispose)
                {
                    Playback();
                }
                else if (m_flag == CommandBufferFlag.MustManualPlaybackOnDispose && m_commands.Count > 0)
                {
                    throw new InvalidOperationException("CommandBuffer must be played back before dispose.");
                }
            }
            finally
            {
                m_disposed = true;
                Pool.Release(this);
            }
        }

        private void Reset()
        {
            m_commands.Clear();
            m_world = null;
            m_flag = CommandBufferFlag.Default;
            m_disposed = true;
        }

        private void EnsureOpen()
        {
            if (m_disposed || m_world == null)
                throw new InvalidOperationException("CommandBuffer is disposed.");
        }

        private static void EnsureValidEntity(Entity entity)
        {
            if (!entity.IsValid)
                throw new InvalidOperationException("Entity is not valid.");
        }

        private static void ValidateFlag(CommandBufferFlag flag)
        {
            if (flag != CommandBufferFlag.AutoPlaybackOnDispose &&
                flag != CommandBufferFlag.DiscardPendingOnDispose &&
                flag != CommandBufferFlag.MustManualPlaybackOnDispose)
            {
                throw new ArgumentOutOfRangeException(nameof(flag), flag, "Unsupported command buffer flag.");
            }
        }
    }
}
