using System.Diagnostics.Contracts;

namespace ChronoJsonDiffPatch;

/// <summary>
/// similar to <see cref="TimeRangePatchChainProduct{TEntityA,TEntityB}"/> but with an arbitrary (user defined) product type <typeparamref name="TProductEntity"/>
/// </summary>
/// <remarks>
/// It's not possible to define generic types with an arbitrary number of type parameters.
/// So instead of creating a type TimeRangePatchChainProduct&lt;T1, T2...Tn&gt; for every n, we encapsulate the n-part behind an interface <see cref="IChainsAndEntities{TProductEntity}"/>.
/// The type that implements the interface is responsible for the actual patching logic.
/// </remarks>
/// <typeparam name="TProductEntity">type that holds the state of n different types at a given point in time</typeparam>
/// <typeparam name="TChainsAndEntities">the initial state and respective chains of a the different types</typeparam>
public abstract class GenericTimeRangePatchChainProduct<TChainsAndEntities, TProductEntity>
    where TChainsAndEntities : IChainsAndEntities<TProductEntity>
{
    /// <summary>
    /// the relevant initial states and chains
    /// </summary>
    private readonly TChainsAndEntities _chainsAndEntities;

    /// <summary>
    /// instantiate the generic chain product by providing an instance of a type that holds all chains and respective initial entities.
    /// </summary>
    /// <returns></returns>
    public GenericTimeRangePatchChainProduct(TChainsAndEntities chainsAndEntities)
    {
        _chainsAndEntities = chainsAndEntities;
    }

    /// <summary>
    /// returns the joint history of all chains involved in <typeparamref name="TChainsAndEntities"/>
    /// </summary>
    /// <returns>an enumerable that contains entries for every relevant date which there is in either or both of the chains</returns>
    [Pure]
    public IEnumerable<TProductEntity> GetAll()
    {
        var result = _chainsAndEntities
            .GetPatchingDates()
            .Where(dt => dt != DateTime.MinValue)
            .Where(dt => dt != DateTime.MaxValue)
            .Distinct()
            .Order()
            .Select(keyDate => _chainsAndEntities.ToProductEntity(keyDate));
        return result;
    }
}
