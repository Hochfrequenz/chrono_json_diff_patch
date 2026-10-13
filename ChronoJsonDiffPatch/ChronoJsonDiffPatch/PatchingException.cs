using Newtonsoft.Json.Linq;

namespace ChronoJsonDiffPatch;

/// <summary>
/// is thrown when something fails to be patched
/// </summary>
/// <remarks>
/// it's basically a wrapper around the inner exception with some helpful debugging information
/// </remarks>
public class PatchingException<TEntity> : Exception
{
    /// <summary>
    /// state of the entity at +/- infinity (before any patch was applied)
    /// </summary>
    public TEntity StateOfEntityBeforeAnyPatch { get; }

    /// <summary>
    /// state of the entity at the point where Applying <see cref="FailedPatch"/> failed
    /// </summary>
    public TEntity StateOfEntityBeforePatch { get; }

    /// <summary>
    /// patch that could not be applied
    /// </summary>
    public JToken? FailedPatch { get; }

    /// <summary>
    /// index of the patch inside the chain
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// instantiate with all the necessary information
    /// </summary>
    /// <param name="stateOfEntityBeforeAnyPatch"></param>
    /// <param name="left"></param>
    /// <param name="patch"></param>
    /// <param name="index"></param>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public PatchingException(
        TEntity stateOfEntityBeforeAnyPatch,
        TEntity left,
        JToken? patch,
        int index,
        string message,
        Exception innerException
    )
        : base(message, innerException)
    {
        StateOfEntityBeforeAnyPatch = stateOfEntityBeforeAnyPatch;
        StateOfEntityBeforePatch = left;
        FailedPatch = patch;
        Index = index;
    }

    /// <inheritdoc />
    public override string Message =>
        $"{base.Message}. Patch (index {Index}): {FailedPatch ?? "<null>"}; State: {StateOfEntityBeforePatch}";
}
