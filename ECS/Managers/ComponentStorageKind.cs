using System;
using CoreECS.Defines;

namespace CoreECS.Managers
{
    internal static class ComponentStorageKind
    {
        public static bool IsSparse<T>() where T : struct, IComponent<T>
            => IsSparse(typeof(T));

        public static bool IsSparse(Type componentType)
        {
            if (componentType == null || !componentType.IsValueType) return false;
            foreach (var iface in componentType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ISparseComponent<>))
                    return true;
            }
            return false;
        }
    }
}
