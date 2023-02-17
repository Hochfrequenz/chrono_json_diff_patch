using System.Diagnostics.Contracts;

namespace ChronoJsonDiffPatch;

/// <summary>
/// Two <see cref="TimeRangePatchChain{TEntity}"/>s can be combined to form "TimeRangePatchChainProduct".
/// The product can be used to create a <see cref="ProductEntity{TEntityA,TEntityB}"/> which tracks changes of both <typeparamref name="TEntityA"/> and  <typeparamref name="TEntityB"/>.
/// </summary>
public class TimeRangePatchChainProduct<TEntityA, TEntityB>
{
    private readonly TimeRangePatchChain<TEntityA> _chainA;
    private readonly TimeRangePatchChain<TEntityB> _chainB;
    private readonly TEntityA _initialStateOfA;
    private readonly TEntityB _initialStateOfB;

    /// <summary>
    /// initialize the product by providing both chains and the respective initial entity states
    /// </summary>
    /// <param name="chainA"></param>
    /// <param name="initialStateOfA"></param>
    /// <param name="chainB"></param>
    /// <param name="initialStateOfB"></param>
    public TimeRangePatchChainProduct(TimeRangePatchChain<TEntityA> chainA, TEntityA initialStateOfA, TimeRangePatchChain<TEntityB> chainB, TEntityB initialStateOfB)
    {
        _chainA = chainA;
        _chainB = chainB;
        _initialStateOfA = initialStateOfA;
        _initialStateOfB = initialStateOfB;
    }

    [Pure]
    private ProductEntity<TEntityA, TEntityB> PatchToDate(DateTimeOffset keyDate)
    {
        var stateOfEntityA = _chainA.PatchToDate(_initialStateOfA, keyDate);
        var stateOfEntityB = _chainB.PatchToDate(_initialStateOfB, keyDate);
        return new ProductEntity<TEntityA, TEntityB>(stateOfEntityA, stateOfEntityB, keyDate);
    }

    /// <summary>
    /// returns the joint history of both chains.
    /// </summary>
    /// <returns>an enumerable that contains entries for every relevant date which there is in either or both of the chains</returns>
    [Pure]
    public IEnumerable<ProductEntity<TEntityA, TEntityB>> GetAll()
    {
        var result = _chainA.GetAll().Select(trp => trp.Start).Union(_chainB.GetAll().Select(trp => trp.Start))
            .Where(dt => dt != DateTime.MinValue)
            .Where(dt => dt != DateTime.MaxValue)
            .Distinct()
            .Order()
            .Select(keyDate => PatchToDate(keyDate));
        return result;
    }
}
