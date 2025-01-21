namespace ChronoJsonDiffPatch;

/// <summary>
/// Assume
/// * There are two key dates: dtA and dtB
/// * dtB &gt; dtA
/// This enum describes if we model the <see cref="TimeRangePatchChain{TEntity}"/> as patches that are applied from earlier to later times or the other way around.
/// </summary>
public enum PatchingDirection
{
    /// <summary>
    /// <see cref="TimeRangePatch.Patch"/>es are modelled as transitions from dtA to dtB.
    /// You could also refer to this as "left to right" on a time axis.
    /// </summary>
    /// <remarks>This is the patching direction that feels "natural".</remarks>
    ParallelWithTime,

    /// <summary>
    /// <see cref="TimeRangePatch.Patch"/>es are modelled as transitions from dtB to dtA.
    /// You could also refer to this as "right to left" on a time axis.
    /// </summary>
    /// <remarks>
    /// Although this doesn't feel as natural as <see cref="ParallelWithTime"/>, it might useful if you store changes to an entity in a database and you always want to have the most recent/youngest state as "full" entity and persist the past as patches.
    /// Then you always start with the youngest entity and patch "backwards" until you reach the point in time/in the past in which you're interested.
    /// </remarks>
    AntiParallelWithTime,
}
