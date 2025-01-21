namespace ChronoJsonDiffPatch;

/// <summary>
/// Two <see cref="TimeRangePatchChain{TEntity}"/>s can be combined to form "TimeRangePatchChainProduct".
/// The product can be used to create a <see cref="ProductEntity{TEntityA,TEntityB}"/> which tracks changes of both <typeparamref name="TEntityA"/> and <typeparamref name="TEntityB"/>.
/// </summary>
public class TimeRangePatchChainProduct<TEntityA, TEntityB>
    : GenericTimeRangePatchChainProduct<
        TwoTypeChainAndEntities<TEntityA, TEntityB>,
        ProductEntity<TEntityA, TEntityB>
    >
{
    /// <summary>
    /// initialize by providing the raw chains and entities involved
    /// </summary>
    /// <param name="entityA"></param>
    /// <param name="chainA"></param>
    /// <param name="entityB"></param>
    /// <param name="chainB"></param>
    public TimeRangePatchChainProduct(
        TimeRangePatchChain<TEntityA> chainA,
        TEntityA entityA,
        TimeRangePatchChain<TEntityB> chainB,
        TEntityB entityB
    )
        : base(new TwoTypeChainAndEntities<TEntityA, TEntityB>(chainA, entityA, chainB, entityB))
    { }
}
