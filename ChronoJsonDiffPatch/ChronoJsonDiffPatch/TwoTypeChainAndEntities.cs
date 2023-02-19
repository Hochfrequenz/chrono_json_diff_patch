using System.Diagnostics.Contracts;

namespace ChronoJsonDiffPatch;

/// <summary>
/// type that encapsulates state and chain of two entities <typeparamref name="TEntityA"/> and <typeparamref name="TEntityB"/>.
/// </summary>
/// <typeparam name="TEntityA"></typeparam>
/// <typeparam name="TEntityB"></typeparam>
public class TwoTypeChainAndEntities<TEntityA, TEntityB> : IChainsAndEntities<ProductEntity<TEntityA, TEntityB>>
{
    private readonly TimeRangePatchChain<TEntityA> _chainA;
    private readonly TimeRangePatchChain<TEntityB> _chainB;
    private readonly TEntityA _initialStateOfA;
    private readonly TEntityB _initialStateOfB;

    /// <summary>
    /// instantiate by providing both initial entities <paramref name="initialStateOfA"/>, <paramref name="initialStateOfB"/> as well as the respective chains <paramref name="chainA"/>, <paramref name="chainB"/>
    /// </summary>
    /// <param name="chainA"></param>
    /// <param name="initialStateOfA"></param>
    /// <param name="chainB"></param>
    /// <param name="initialStateOfB"></param>
    public TwoTypeChainAndEntities(TimeRangePatchChain<TEntityA> chainA, TEntityA initialStateOfA, TimeRangePatchChain<TEntityB> chainB, TEntityB initialStateOfB)
    {
        _chainA = chainA;
        _chainB = chainB;
        _initialStateOfA = initialStateOfA;
        _initialStateOfB = initialStateOfB;
    }

    /// <inheritdoc />
    [Pure]
    public ProductEntity<TEntityA, TEntityB> ToProductEntity(DateTimeOffset keyDate)
    {
        var stateOfEntityA = _chainA.PatchToDate(_initialStateOfA, keyDate);
        var stateOfEntityB = _chainB.PatchToDate(_initialStateOfB, keyDate);
        return new ProductEntity<TEntityA, TEntityB>(stateOfEntityA, stateOfEntityB, keyDate);
    }

    /// <inheritdoc />
    [Pure]
    public IEnumerable<DateTime> GetPatchingDates()
    {
        return _chainA.GetAll().Select(trp => trp.Start)
            .Union(_chainB.GetAll().Select(trp => trp.Start))
            .Distinct();
    }
}
