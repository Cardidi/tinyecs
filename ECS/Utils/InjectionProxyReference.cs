using CoreECS.Defines;

namespace CoreECS.Utils
{
    /// <summary>
    /// Holds the live <see cref="IInjectionProxy"/> after the service provider is built.
    /// </summary>
    internal sealed class InjectionProxyReference
    {
        public IInjectionProxy Value;
    }
}
