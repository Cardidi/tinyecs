using System;
using System.Runtime.Serialization;
using CoreECS.Defines;
using Microsoft.Extensions.DependencyInjection;

namespace CoreECS.Utils
{
    /// <summary>
    /// <see cref="IInjectionProxy"/> that activates objects via <see cref="Injector"/> with
    /// optional constructor dependencies resolved from a built <see cref="IServiceProvider"/>.
    /// </summary>
    public sealed class InjectorInjectionProxy : IInjectionProxy
    {
        private readonly IServiceProvider m_serviceProvider;
        private readonly Injector m_injector;

        public InjectorInjectionProxy(IServiceProvider serviceProvider, Injector injector)
        {
            Assertion.ArgumentNotNull(serviceProvider);
            Assertion.ArgumentNotNull(injector);
            m_serviceProvider = serviceProvider;
            m_injector = injector;
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

            var instance = FormatterServices.GetUninitializedObject(objectType);
            m_injector.Register(instance);
            ConstructorInjection.Inject(m_serviceProvider, m_injector.Instances, instance);
            return instance;
        }

        public T CreateObject<T>()
        {
            return (T)CreateObject(typeof(T));
        }
    }
}
