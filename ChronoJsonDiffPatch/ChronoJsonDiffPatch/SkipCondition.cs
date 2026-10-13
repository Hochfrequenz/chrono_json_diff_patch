namespace ChronoJsonDiffPatch;

/// <summary>
/// models cases in which a patch should be skipped / not be applied to an entity
/// </summary>
/// <remarks>
/// Usually this shouldn't be necessary to use if everything is already. But if your data are corrupted somehow, this might be a way to still apply some patches to you data and only skip some instead of all.
/// Use this with caution (but of course we know: as soon as it's possible to ignore errors, we'll use it.
/// The state of the entity after a patch has been skipped is not well-defined.
/// It _might_ be somewhat like what you'd expect but all guarantees are gone.
/// It's the pandora's box of patching you're opening here.
/// Be sure to monitor the state of your entities after applying patches with skipped patches and <see cref="TimeRangePatchChain{TEntity}.PatchesHaveBeenSkipped"/>.
/// </remarks>
/// <typeparam name="TEntity">the entity to which the patches shall be applied</typeparam>
public interface ISkipCondition<in TEntity>
{
    /// <summary>
    /// If applying <paramref name="failedPatch"/> to <paramref name="initialEntity"/> fails with <paramref name="errorWhilePatching"/>, the patch should be skipped.
    /// </summary>
    /// <param name="initialEntity">state before applying the patch <paramref name="failedPatch"/> (but not necessarily before apply any patch in the chain)</param>
    /// <param name="errorWhilePatching">any error that occurred</param>
    /// <param name="failedPatch">the patch that lead to the exception <paramref name="errorWhilePatching"/>; null if the error occurred during final deserialization after all patches were applied</param>
    /// <returns>true if the patch should be skipped</returns>
    public bool ShouldSkipPatch(
        TEntity initialEntity,
        TimeRangePatch? failedPatch,
        Exception errorWhilePatching
    );
}
