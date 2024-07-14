namespace ChronoJsonDiffPatch;

/// <summary>
/// skips patches where an item inside a list is modified or removed that is not present in the initial list (because of corrupted data)
/// </summary>
/// <typeparam name="TEntity">the entity to be patched</typeparam>
/// <typeparam name="TListItem">the items inside the list</typeparam>
public class SkipPatchesWithUnmatchedListItems<TEntity, TListItem> : ISkipCondition<TEntity>
{
    private readonly Func<TEntity, List<TListItem>?> _listAccessor;

    /// <summary>
    /// provide a way to access the list which index is out of range
    /// </summary>
    /// <param name="listAccessor"></param>
    public SkipPatchesWithUnmatchedListItems(Func<TEntity, List<TListItem>?> listAccessor)
    {
        _listAccessor = listAccessor;
    }
    /// <summary>
    /// <inheritdoc cref="ISkipCondition{TEntity}"/>
    /// </summary>
    public bool ShouldSkipPatch(TEntity initialEntity, TimeRangePatch failedPatch, Exception errorWhilePatching)
    {
        if (errorWhilePatching is not ArgumentOutOfRangeException)
        {
            return false;
        }
        var list = _listAccessor(initialEntity);
        if (list is null)
        {
            return false;
        }
        // todo: theoretically I could
        // 1. check the json attributes of the list property,
        // 2. then inspect the failedPatch.Patch and then
        // 3. see if the error _really_ originates from there
        // but for now this is a good enough solution
        return true;
    }
}
