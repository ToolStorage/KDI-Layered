namespace Kylin.DI.Layered
{
    public interface IDomainServiceLayer : IDependencyObject, IInjectable
    {
    }

    /// <summary>
    /// DomainService that declares ownership of a specific Data layer type.
    /// Only owners may invoke <c>[OwnerOnly]</c>-marked members on <typeparamref name="TOwnedData"/>;
    /// the <c>KDI003</c> Roslyn analyzer rule enforces this at compile time.
    /// Implement multiple constructed variants to own more than one Data type.
    /// </summary>
    /// <typeparam name="TOwnedData">The Data layer type this DomainService is permitted to mutate.</typeparam>
    public interface IDomainServiceLayer<TOwnedData> : IDomainServiceLayer
        where TOwnedData : IDataLayer
    {
    }
}
