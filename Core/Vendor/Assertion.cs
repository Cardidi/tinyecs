using System.Runtime.CompilerServices;

namespace TinyECS.Core.Vendor
{
    public static class Assertion
    {
        public static void IsTrue(bool condition, string message = null, [CallerMemberName] string memberName = null)
        {
            if (!condition)
            {
                string errorMessage = message ?? memberName;
                throw new InvalidOperationException(errorMessage);
            }
        }

        public static void IsFalse(bool condition, string message = null, [CallerMemberName] string memberName = null)
        {
            if (condition)
            {
                string errorMessage = message ?? memberName;
                throw new InvalidOperationException(errorMessage);
            }
        }

        public static void IsNull(object obj, string message = null, [CallerMemberName] string memberName = null)
        {
            if (obj != null)
            {
                string errorMessage = message ?? memberName;
                throw new InvalidOperationException(errorMessage);
            }
        }

        public static void IsNotNull(object obj, string message = null, [CallerMemberName] string memberName = null)
        {
            if (obj == null)
            {
                string errorMessage = message ?? memberName;
                throw new InvalidOperationException(errorMessage);
            }
        }
    }
}