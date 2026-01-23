using System;
using System.Collections.Generic;
using System.Reflection;

namespace TinyECS.Utils
{
    /// <summary>
    /// Simple constructor-based dependency injector.
    /// </summary>
    public sealed class Injector
    {
        private readonly List<object> m_instances = new();
        
        public IReadOnlyList<object> Instances => m_instances;
        
        /// <summary>
        /// Register an instance for dependency injection by type.
        /// </summary>
        public void Register(object instance)
        {
            Assertion.IsNotNull(instance);
            m_instances.Add(instance);
        }
        
        /// <summary>
        /// Inject dependencies into the provided instance by calling its constructor.
        /// </summary>
        public void InjectConstructor(object instance)
        {
            Assertion.IsNotNull(instance);
            
            Type type = instance.GetType();
            
            // Get all public constructors
            ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            
            if (constructors.Length == 0)
            {
                throw new InvalidOperationException($"No public constructors found for type {type.Name}.");
            }
            
            // Sort constructors by parameter count from most to least
            Array.Sort(constructors, (a, b) => b.GetParameters().Length - a.GetParameters().Length);

            using (ListPool<object>.Get(out var resolvedParameters))
            {
                ConstructorInfo injectConstructor = null;
                
                // Try each constructor in order of parameter count (most to least)
                foreach (var constructor in constructors)
                {
                    try
                    {
                        ParameterInfo[] parameters = constructor.GetParameters();
                        resolvedParameters.Clear();

                        // Try to resolve all parameters
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            var p = parameters[i];
                            if (!_resolveParameter(parameters[i], out var resolved))
                            {
                                if (!p.HasDefaultValue) break;
                                resolved = p.DefaultValue;
                            }
                            
                            resolvedParameters.Add(resolved);
                        }
                        
                        if (parameters.Length != resolvedParameters.Count) continue;

                        // If we got here, all parameters can be resolved
                        injectConstructor = constructor;
                        break;
                    }
                    catch
                    {
                        // Ignore constructors that we can't resolve parameters for
                    }
                }

                if (injectConstructor == null)
                {
                    throw new InvalidOperationException(
                        $"No suitable constructor found for type {type.Name} with resolvable parameters.");
                }

                // Call the constructor to inject dependencies
                injectConstructor.Invoke(instance, resolvedParameters.ToArray());
            }
        }
        
        /// <summary>
        /// Resolve a parameter by its type.
        /// </summary>
        private bool _resolveParameter(ParameterInfo parameter, out object resolved)
        {
            Type parameterType = parameter.ParameterType;
            
            // Try to find the best matching instance
            object bestMatch = null;
            int bestMatchScore = -1;
            
            foreach (var instance in m_instances)
            {
                Type instanceType = instance.GetType();
                
                // Exact type match is the best
                if (instanceType == parameterType)
                {
                    resolved = instance;
                    return true;
                }
                
                // Check if the instance is assignable to the parameter type
                if (parameterType.IsAssignableFrom(instanceType))
                {
                    // Calculate match score - more derived types get higher scores
                    int score = _calculateInheritanceDepth(instanceType, parameterType);
                    if (score > bestMatchScore)
                    {
                        bestMatchScore = score;
                        bestMatch = instance;
                    }
                }
            }
            
            if (bestMatch != null)
            {
                resolved = bestMatch;
                return true;
            }
            
            resolved = null;
            return false;
        }
        
        /// <summary>
        /// Calculate the inheritance depth between two types.
        /// </summary>
        private int _calculateInheritanceDepth(Type derivedType, Type baseType)
        {
            int depth = 0;
            Type currentType = derivedType;
            
            while (currentType != null && currentType != baseType)
            {
                currentType = currentType.BaseType;
                depth++;
            }
            
            // If we reached null, the types are not in the same inheritance hierarchy
            return currentType == baseType ? depth : -1;
        }
    }
}