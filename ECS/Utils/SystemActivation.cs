using System;
using System.Runtime.Serialization;
using CoreECS.Defines;
using Microsoft.Extensions.DependencyInjection;

namespace CoreECS.Utils
{
    /// <summary>
    /// Activates <see cref="ISystem"/> instances outside of the DI service graph.
    /// </summary>
    internal static class SystemActivation
    {
        public static ISystem Activate(IServiceProvider serviceProvider, Injector runtimeRegistry, Type systemType)
        {
            Assertion.ArgumentNotNull(serviceProvider);
            Assertion.ArgumentNotNull(runtimeRegistry);
            Assertion.ArgumentNotNull(systemType);
            Assertion.IsParentTypeTo<ISystem>(systemType);

            if (serviceProvider.GetService(systemType) != null)
            {
                throw new InvalidOperationException(
                    $"Type {systemType.Name} must not be registered in the service collection.");
            }

            var instance = (ISystem)FormatterServices.GetUninitializedObject(systemType);
            ConstructorInjection.Inject(serviceProvider, runtimeRegistry.Instances, instance);
            return instance;
        }
    }
}
