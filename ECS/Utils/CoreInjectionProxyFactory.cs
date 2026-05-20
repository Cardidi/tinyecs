using CoreECS.Defines;
using Microsoft.Extensions.DependencyInjection;

namespace CoreECS.Utils
{
    /// <summary>
    /// Builds <see cref="CoreInjectionProxy"/> from a configured service collection.
    /// </summary>
    public sealed class CoreInjectionProxyFactory : IInjectionProxyFactory
    {
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
            var proxy = new CoreInjectionProxy(provider);
            holder.Value = proxy;
            return proxy;
        }
    }
}
