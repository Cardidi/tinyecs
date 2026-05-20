using System;
using CoreECS.Defines;
using Microsoft.Extensions.DependencyInjection;

namespace CoreECS
{
    internal class BuiltinInjectionProxy : IInjectionProxy
    {
        public IServiceProvider ServiceProvider { get; }

        public BuiltinInjectionProxy(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public object CreateObject(Type objectType)
        {
            return ActivatorUtilities.CreateInstance(ServiceProvider, objectType);
        }

        public T CreateObject<T>()
        {
            return ActivatorUtilities.CreateInstance<T>(ServiceProvider);
        }
    }

    internal class BuiltinInjectionProxyFactory : IInjectionProxyFactory
    {
        public static IInjectionProxyFactory Instance = new BuiltinInjectionProxyFactory();
        
        public IServiceCollection CreateServiceCollection()
        {
            return new ServiceCollection();
        }

        public IInjectionProxy CreateProxy(IServiceCollection collection)
        {
            IInjectionProxy proxy = null;
            collection.AddSingleton<IInjectionProxy>(_ => proxy);
            var provider = collection.BuildServiceProvider();
            proxy = new BuiltinInjectionProxy(provider);
            return proxy;
        }
    }
}
