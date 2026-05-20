using System;
using CoreECS.Defines;
using Microsoft.Extensions.DependencyInjection;

namespace CoreECS.Utils
{
    /// <summary>
    /// Default <see cref="IInjectionProxy"/> backed by a built service provider.
    /// </summary>
    public sealed class CoreInjectionProxy : IInjectionProxy
    {
        private readonly IServiceProvider m_serviceProvider;

        public CoreInjectionProxy(IServiceProvider serviceProvider)
        {
            Assertion.ArgumentNotNull(serviceProvider);
            m_serviceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider => m_serviceProvider;

        public object CreateObject(Type objectType)
        {
            Assertion.ArgumentNotNull(objectType);

            if (typeof(ISystem).IsAssignableFrom(objectType))
            {
                throw new InvalidOperationException(
                    $"Type {objectType.Name} implements {nameof(ISystem)} and must not be created via {nameof(IInjectionProxy)}.");
            }

            return ActivatorUtilities.GetServiceOrCreateInstance(m_serviceProvider, objectType);
        }

        public T CreateObject<T>()
        {
            return (T)CreateObject(typeof(T));
        }
    }
}
