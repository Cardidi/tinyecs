using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CoreECS.Utils
{
    /// <summary>
    /// Constructor injection for uninitialized instances.
    /// </summary>
    internal static class ConstructorInjection
    {
        public static void Inject(IServiceProvider serviceProvider, IReadOnlyList<object> runtimeInstances, object instance)
        {
            Assertion.ArgumentNotNull(instance);

            var type = instance.GetType();
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

            if (constructors.Length == 0)
            {
                throw new InvalidOperationException($"No public constructors found for type {type.Name}.");
            }

            Array.Sort(constructors, (a, b) => b.GetParameters().Length - a.GetParameters().Length);

            using (ListPool<object>.Get(out var resolvedParameters))
            {
                ConstructorInfo injectConstructor = null;

                foreach (var constructor in constructors)
                {
                    try
                    {
                        var parameters = constructor.GetParameters();
                        resolvedParameters.Clear();

                        for (var i = 0; i < parameters.Length; i++)
                        {
                            var p = parameters[i];
                            if (!_tryResolveParameter(serviceProvider, runtimeInstances, p, out var resolved))
                            {
                                if (!p.HasDefaultValue) break;
                                resolved = p.DefaultValue;
                            }

                            resolvedParameters.Add(resolved);
                        }

                        if (parameters.Length != resolvedParameters.Count) continue;

                        injectConstructor = constructor;
                        break;
                    }
                    catch
                    {
                        // Try next constructor candidate.
                    }
                }

                if (injectConstructor == null)
                {
                    throw new InvalidOperationException(
                        $"No suitable constructor found for type {type.Name} with resolvable parameters.");
                }

                injectConstructor.Invoke(instance, resolvedParameters.ToArray());
            }
        }

        private static bool _tryResolveParameter(
            IServiceProvider serviceProvider,
            IReadOnlyList<object> runtimeInstances,
            ParameterInfo parameter,
            out object resolved)
        {
            var parameterType = parameter.ParameterType;

            if (serviceProvider != null)
            {
                resolved = serviceProvider.GetService(parameterType);
                if (resolved != null) return true;
            }

            if (runtimeInstances != null)
            {
                foreach (var instance in runtimeInstances)
                {
                    var instanceType = instance.GetType();

                    if (instanceType == parameterType)
                    {
                        resolved = instance;
                        return true;
                    }

                    if (parameterType.IsAssignableFrom(instanceType))
                    {
                        resolved = instance;
                        return true;
                    }
                }
            }

            resolved = null;
            return false;
        }
    }
}
