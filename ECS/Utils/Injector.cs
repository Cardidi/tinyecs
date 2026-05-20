using System;
using System.Collections.Generic;
namespace CoreECS.Utils
{
    /// <summary>
    /// Simple constructor-based dependency injector.
    /// This class provides basic dependency injection functionality by resolving constructor parameters
    /// from registered instances.
    /// </summary>
    public sealed class Injector
    {
        /// <summary>
        /// List of registered instances available for injection.
        /// </summary>
        private readonly List<object> m_instances = new();
        
        /// <summary>
        /// Gets a read-only view of the registered instances.
        /// </summary>
        public IReadOnlyList<object> Instances => m_instances;
        
        /// <summary>
        /// Registers an instance for dependency injection by type.
        /// </summary>
        /// <param name="instance">The instance to register</param>
        public void Register(object instance)
        {
            Assertion.ArgumentNotNull(instance);
            m_instances.Add(instance);
        }
        
        /// <summary>
        /// Injects dependencies into the provided instance by calling its constructor.
        /// </summary>
        /// <param name="instance">The instance to inject dependencies into</param>
        public void InjectConstructor(object instance)
        {
            ConstructorInjection.Inject(null, m_instances, instance);
        }
    }
}