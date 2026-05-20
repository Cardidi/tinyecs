using CoreECS.Defines;
using Microsoft.Extensions.DependencyInjection;

namespace CoreECS.Utils
{
    /// <summary>
    /// Builds <see cref="BuiltinInjectionProxy"/> for worlds without a third-party DI container.
    /// </summary>
    public sealed class BuiltinInjectionProxyFactory : IInjectionProxyFactory
    {
        private readonly Injector m_injector;

        public BuiltinInjectionProxyFactory(Injector injector)
        {
            Assertion.ArgumentNotNull(injector);
            m_injector = injector;
        }

        public IServiceCollection CreateServiceCollection()
        {
            return new ServiceCollection();
        }

        public IInjectionProxy CreateProxy(IServiceCollection collection)
        {
            Assertion.ArgumentNotNull(collection);

            var holder = new InjectionProxyReference();
            collection.AddSingleton(holder);
            collection.AddSingleton<IInjectionProxy>(sp => sp.GetRequiredService<InjectionProxyReference>().Value);

            var provider = collection.BuildServiceProvider();
            var proxy = new BuiltinInjectionProxy(provider, m_injector);
            holder.Value = proxy;
            return proxy;
        }
    }
}
