namespace ChronoJsonDiffPatch;

/// <summary>
/// A ProductEntity is a composite of both an <typeparamref name="TEntityA"/> and a <typeparamref name="TEntityB"/>.
/// </summary>
/// <remarks>This is a shortcut for a <see cref="Tuple{TEntityA, TEntityB, DateTimeOffset}"/></remarks>
/// <typeparam name="TEntityA"></typeparam>
/// <typeparam name="TEntityB"></typeparam>
public class ProductEntity<TEntityA, TEntityB>
{
    /// <summary>
    /// describes the date at which <typeparamref name="TEntityA"/> has state <see cref="EntityA"/> and <typeparamref name="TEntityB"/> has state <see cref="EntityB"/>
    /// </summary>
    public DateTimeOffset KeyDate { get; }

    /// <summary>
    /// an instance of <typeparamref name="TEntityA"/> @ <see cref="KeyDate"/>
    /// </summary>
    public TEntityA EntityA { get; }

    /// <summary>
    /// an instance of <typeparamref name="TEntityB"/> @ <see cref="KeyDate"/>
    /// </summary>
    public TEntityB EntityB { get; }

    /// <summary>
    /// initialize the product by providing the three relevant values
    /// </summary>
    /// <param name="entityA"><typeparamref name="TEntityA"/> at <paramref name="keyDate"/></param>
    /// <param name="entityB"><typeparamref name="TEntityB"/> at <paramref name="keyDate"/></param>
    /// <param name="keyDate">date which is relevant</param>
    public ProductEntity(TEntityA entityA, TEntityB entityB, DateTimeOffset keyDate)
    {
        EntityA = entityA;
        EntityB = entityB;
        KeyDate = keyDate;
    }
}
