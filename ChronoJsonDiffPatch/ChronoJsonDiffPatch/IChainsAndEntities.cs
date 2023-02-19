namespace ChronoJsonDiffPatch;

/// <summary>
/// This interface is an abstraction layer that encapsulates an arbitrary number of entities and their respective chains
/// </summary>
/// <typeparam name="TProductEntity">a product entity is holds the state of all involved entities. Think of it as something like a n-sized tuple of entities and chains.</typeparam>
public interface IChainsAndEntities<out TProductEntity>
{
    /// <summary>
    /// patch the <typeparamref name="TProductEntity"/> to the state @<paramref name="keyDate"/>
    /// </summary>
    /// <param name="keyDate"></param>
    /// <returns>state of the <typeparamref name="TProductEntity"/> at <paramref name="keyDate"/></returns>
    public TProductEntity ToProductEntity(DateTimeOffset keyDate);

    /// <summary>
    /// returns those dates at which patches are present
    /// </summary>
    /// <returns></returns>
    public IEnumerable<DateTime> GetPatchingDates();
}
