using System;

namespace Kylin.DI.Layered
{
    /// <summary>
    /// Marks a Data layer member as owner-restricted: only a class that declares
    /// <see cref="IDomainServiceLayer{TOwnedData}"/> ownership of the declaring Data type
    /// may invoke it. Apply to mutator methods, or to a property setter when using
    /// per-accessor attribute syntax (<c>public int X { get; [OwnerOnly] set; }</c>).
    /// Enforced at compile time by the <c>KDI003</c> Roslyn analyzer rule; self-calls
    /// from inside the declaring Data class (and its subclasses) are always allowed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class OwnerOnlyAttribute : Attribute
    {
    }
}
