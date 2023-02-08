using Itenso.TimePeriod;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChronoJsonDiffPatch;

/// <summary>
/// Assume
/// * There are two key dates: dtA and dtB
/// * dtB &gt; dtA
/// * There is a patchA at dtA that puts our entity in state A (assuming you use <see cref="PatchingDirection.ParallelWithTime"/>).
/// * There is a patchB at dtB that puts our entity in state B (assuming you use <see cref="PatchingDirection.ParallelWithTime"/>).
/// If the patching order is now: (patchB, patchA), what is the desired state at dtB?
/// This enums allows you to distinguish the behaviour of <see cref="TimeRangePatchChain{TEntity}.Add(TEntity,TEntity,System.DateTimeOffset,System.Nullable{ChronoJsonDiffPatch.FuturePatchBehaviour})"/>
/// </summary>
public enum FuturePatchBehaviour
{
    /// <summary>
    /// The future (&gt;= dtB) shall have state A: (the previous future state B is overwritten)
    /// </summary>
    OverwriteTheFuture,
    /// <summary>
    /// The future (&gt;= dtB) shall have state B: the second patchA only lasts in the interval between [A,B)
    /// </summary>
    KeepTheFuture,
}

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
    AntiparallelWithTime,
}

/// <summary>
/// A collection of <see cref="TimeRangePatch"/>es that implements <see cref="ITimePeriodChain"/>
/// </summary>
/// <typeparam name="TEntity">a serializable entity whose changes shall be tracked as JsonDiffPatches</typeparam>
/// <remarks>We inherit from <see cref="TimePeriodChain"/> because this guarantees that there are no gaps between two <see cref="TimeRangePatch"/>es</remarks>
public class TimeRangePatchChain<TEntity> : TimePeriodChain
{
    /// <summary>
    /// uses system.text
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    protected static string DefaultSerializer(TEntity entity)
    {
        return System.Text.Json.JsonSerializer.Serialize(entity);
    }

    /// <summary>
    /// uses system.text
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    protected static TEntity DefaultDeSerializer(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<TEntity>(json)!;
    }
    private readonly Func<TEntity, string> _serializer;
    private readonly Func<string, TEntity> _deserializer;
    private readonly PatchingDirection _patchingDirection;
    /// <summary>
    /// initialize the collection by providing a list of time periods
    /// </summary>
    /// <param name="timeperiods"></param>
    /// <param name="patchingDirection"><see cref="PatchingDirection"/></param>
    /// <param name="serializer">a function that is able to serialize <typeparamref name="TEntity"/></param>
    /// <param name="deserializer">a function that is able to deserialize <typeparamref name="TEntity"/></param>
    public TimeRangePatchChain(IEnumerable<TimeRangePatch>? timeperiods = null, PatchingDirection patchingDirection = PatchingDirection.ParallelWithTime, Func<TEntity, string>? serializer = null, Func<string, TEntity>? deserializer = null) : base(timeperiods ?? new List<TimeRangePatch> { })
    {
        _serializer = serializer ?? DefaultSerializer;
        _deserializer = deserializer ?? DefaultDeSerializer;
        _patchingDirection = patchingDirection;
    }

    /// <summary>
    /// returns all Items ever added
    /// </summary>
    /// <returns></returns>
    public IEnumerable<TimeRangePatch> GetAll()
    {
        return this.Cast<TimeRangePatch>()
            .OrderBy(trp => trp.From)
            .ThenBy(trp => trp.End);
    }

    /// <summary>
    /// Returns true iff the chain contains an item that has a <see cref="TimeRange.Start"/> date whose start doesn't differ more than <paramref name="graceTicks"/> from <paramref name="start"/>.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="graceTicks"></param>
    /// <returns></returns>
    public bool Contains(DateTimeOffset start, uint graceTicks = 1000)
    {
        if (graceTicks == 0)
        {
            return GetAll().Any(ze => ze.Start.ToUniversalTime() == start.UtcDateTime);
        }

        // the key times somehow differ by <1000 Ticks sometimes, this is probably because of ORM internals
        return GetAll().Any(ze => Math.Abs((ze.Start.ToUniversalTime() - start.UtcDateTime).Ticks) <= graceTicks);
    }

    /// <summary>
    /// Adds a <see cref="TimeRangePatch"/> to the <see cref="TimePeriodChain"/>.
    /// The patch is constructed using the initial state of the entity <paramref name="initialEntity"/> and the state <paramref name="changedEntity"/> which should come into effect at the specified <paramref name="moment"/>.
    /// This method loops over all existing patches and adds another differential patch that describes the difference between the state of the entity closest to <paramref name="moment"/> and <paramref name="moment"/>.
    /// </summary>
    /// <param name="initialEntity">Initial state of the Entity at the beginning of time.</param>
    /// <param name="changedEntity">The state that the entity should have at <paramref name="moment"/></param>
    /// <param name="moment">a point in time at which the entity shall have the state <paramref name="changedEntity"/></param>
    /// <param name="futurePatchBehaviour">
    /// When there already exists any patch whose <see cref="TimeRange.Start"/> is &gt;= <paramref name="moment"/> then you have to specify how the requested "change in the past" shall behave.
    /// For details see the docstrings of <see cref="FuturePatchBehaviour"/>.
    /// In the straight forward case that <paramref name="moment"/> is later than all existing patches start dates, you can leave the field empty/default to null.
    /// </param>
    /// <exception cref="ArgumentException">This shall be removed in the future</exception>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="NotImplementedException"></exception>
    public void Add(TEntity initialEntity, TEntity changedEntity, DateTimeOffset moment, FuturePatchBehaviour? futurePatchBehaviour = null)
    {
        if (Contains(moment))
        {
            throw new ArgumentException("You must not add a patch at a date for which there is already a patch");
            // the behaviour is undefined as of now
        }
        var jdp = new JsonDiffPatch();
        JToken patchAtKeyDate;
        var changedToken = JToken.Parse(_serializer(changedEntity));
        if (Count == 0) // seems like this entity has no tracked changes so far
        {
            var initialToken = JToken.Parse(_serializer(initialEntity));

            // We initially add 2 patches:
            // 1) an empty patch from the beginning of time (-infinity) up to the moment at which the changes is applied
            //    this means: the entity is unchanged from -infinity up to the given moment.
            var trpFromBeginningOfTimeUntilKeyDate = new TimeRangePatch(from: DateTimeOffset.MinValue, patch: null, to: moment.UtcDateTime);
            base.Add(trpFromBeginningOfTimeUntilKeyDate);
            // 2) a patch that describes the changes that happen at the moment
            // their order is such that the index[0] of the list contains the most recent change and index[-1] starts at -infinity
            patchAtKeyDate = jdp.Diff(initialToken, changedToken);
            var trpSinceKeyDate = new TimeRangePatch(from: moment.UtcDateTime, patch: System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(patchAtKeyDate)), to: null);
            base.Add(trpSinceKeyDate);
            return;
        }

        var upToDateToken = JToken.Parse(_serializer(PatchToDate(initialEntity, moment)));
        patchAtKeyDate = jdp.Diff(upToDateToken, changedToken);
        // there are already patches present
        // first add a patch that starts at the change moment and end at +infinity
        var patchToBeAdded = new TimeRangePatch(from: moment.UtcDateTime, patch: System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(patchAtKeyDate)), to: null);

        bool anyExistingSliceStartsLater = GetAll().Any(trp => trp.Start > patchToBeAdded.Start);
        if (anyExistingSliceStartsLater)
        {
            switch (futurePatchBehaviour)
            {
                case null:
                    throw new ArgumentNullException(nameof(futurePatchBehaviour),
                        $"There already exists any patch whose TimeRange.Start is &gt;= {moment}! You have to specify how the requested \"change in the past\" shall behave");
                case FuturePatchBehaviour.KeepTheFuture:
                    {
                        AddAndKeepTheFuture(initialEntity, patchToBeAdded, changedToken, moment);
                        break;
                    }
                case FuturePatchBehaviour.OverwriteTheFuture:
                    {
                        AddAndOverrideTheFuture(patchToBeAdded);
                        break;
                    }
            }
        }
        else
        {
            ((TimeRangePatch)Last).ShrinkEndTo(patchToBeAdded.Start);
            Add(patchToBeAdded);
        }
    }

    private void AddAndKeepTheFuture(TEntity initialEntity, TimeRangePatch patchToBeAdded, JToken changedToken, DateTimeOffset moment)
    {
        var indexAtWhichThePatchShallBeAdded = IndexOf(GetAll().Last(trp => trp.Start.ToUniversalTime() <= moment.UtcDateTime)) + 1;


        // We shrink the patch to be added such that it does not overwrite existing future patches ("Keep the Future").
        // So we make it end where the next patch starts.
        var firstExistingOverlappingPatch = GetAll().First(trp => trp.Start > patchToBeAdded.Start);
        var intersection = firstExistingOverlappingPatch.GetIntersection(patchToBeAdded);
        patchToBeAdded.ShrinkEndTo(new DateTimeOffset(intersection.Start).UtcDateTime);

        //we need to modify the existing patch itself because its predecessor changed
        var tokenAtIntersectionStart =
            JToken.Parse(_serializer(PatchToDate(initialEntity, intersection.Start.ToUniversalTime())));
        var updatedPatch = new JsonDiffPatch().Diff(changedToken, tokenAtIntersectionStart);
        firstExistingOverlappingPatch.Patch =
            System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(updatedPatch));

        var subtractor = new TimePeriodSubtractor<TimeRangePatch>();
        ITimePeriodCollection existingPatchesWithoutTheRangeCoveredByThePatchToBeAdded =
            subtractor.SubtractPeriods(this, new TimePeriodCollection { patchToBeAdded });
        if (existingPatchesWithoutTheRangeCoveredByThePatchToBeAdded.Count == Count)
        {
            var n = -1;
            foreach (var item in existingPatchesWithoutTheRangeCoveredByThePatchToBeAdded)
            {
                n += 1;
                ((TimeRangePatch)this[n])
                    .ShrinkTo(item); // shrink them all such there is exactly patchToBeAdded.Duration space left (somewhere in the middle)
            }
        }
        else
        {
            var itemsLeftOfTheGap = existingPatchesWithoutTheRangeCoveredByThePatchToBeAdded.Where(p => p.Start < patchToBeAdded.Start);
            bool anythingHasBeenShrunk = false;
            foreach (var itemLeftOfTheGap in itemsLeftOfTheGap)
            {
                if (GetAll().SingleOrDefault(p => p.Start == itemLeftOfTheGap.Start && p.End >= itemLeftOfTheGap.End) is
                    { } aPatch)
                {
                    aPatch.ShrinkTo(itemLeftOfTheGap);
                    anythingHasBeenShrunk = true;
                }
            }

            if (!anythingHasBeenShrunk)
            {
                GetAll().Last(p => p.Start < patchToBeAdded.Start)
                    .ShrinkEndTo(itemsLeftOfTheGap.First(p => p.End >= patchToBeAdded.Start).End);
            }
        }

        var thoseItemsAfterTheGap = GetAll().Where(s => s.Start > patchToBeAdded.Start);
        foreach (var itemAfterGap in thoseItemsAfterTheGap)
        {
            itemAfterGap.Move(-patchToBeAdded.Duration);
        }


        bool startHasToBeShifted = indexAtWhichThePatchShallBeAdded < Count - 1 && !HasStart; // this triggers the CheckSpaceBefore() check
        if (startHasToBeShifted)
        {
            ((TimeRangePatch)First).ShrinkStartTo((DateTimeOffset.MinValue + patchToBeAdded.Duration).UtcDateTime);
        }

        Insert(indexAtWhichThePatchShallBeAdded, patchToBeAdded);
        if (startHasToBeShifted)
        {
            // don't ask. it passes the tests. that's all I wished for today
            var itemsLeftOfTheAddedPatch = this.Where(p => p.Start < patchToBeAdded.Start).Cast<TimeRangePatch>();
            foreach (var itemLeftOfTheAddedPatch in itemsLeftOfTheAddedPatch)
            {
                // OK... someone explain me this behaviour:
                // If, before the insert, I move the items _after_ the keydate to the left,
                // then, after the insert I have to move items _before_ the keydate to the right.
                // I think this line just fixes symptoms. The causes are elsewhere.
                // this is purely testdriven... maybe tobias is right and we should just write the timeperiod code by ourselfs ;)
                itemLeftOfTheAddedPatch.Move(patchToBeAdded.Duration);
            }
            if (HasStart)
            {
                ((TimeRangePatch)First).ExpandStartTo(DateTimeOffset.MinValue.UtcDateTime);
            }
            else
            {
                ((TimeRangePatch)First).ExpandEndTo(First.End + patchToBeAdded.Duration);
                ((TimeRange)this[1]).ShrinkStartTo(First.End);
            }
        }
    }

    private void AddAndOverrideTheFuture(TimeRangePatch patchToBeAdded)
    {
        var futureOverlappingPatchIndexes = GetAll().Where(trp => trp.Start >= patchToBeAdded.Start).Select(IndexOf);
        foreach (var futureIndex in futureOverlappingPatchIndexes.Reverse())
        {
            RemoveAt(futureIndex);
        }

        var lastOverlappingPatchWhichIsNotDeleted = GetAll().Last(p => p.OverlapsWith(patchToBeAdded));
        lastOverlappingPatchWhichIsNotDeleted.ShrinkEndTo(patchToBeAdded.Start);
        Add(patchToBeAdded);
    }


    /// <summary>
    /// start at <paramref name="initialEntity"/> at beginning of time.
    /// Then apply all the patches up to <paramref name="keyDate"/> and return the state of the entity at <paramref name="keyDate"/>
    /// </summary>
    /// <param name="initialEntity">the state of <typeparamref name="TEntity"/> at the beginning of time</param>
    /// <param name="keyDate">the date up to which you'd like to apply the patches</param>
    /// <returns>the state of the entity after all the patches up to <paramref name="keyDate"/> have been applied</returns>
    public TEntity PatchToDate(TEntity initialEntity, DateTimeOffset keyDate)
    {
        var jdp = new JsonDiffPatch();
        var left = JToken.Parse(_serializer(initialEntity));

        foreach (var existingPatch in this.GetAll()
                     .Where(p => ((p.Start == DateTime.MinValue && keyDate != DateTimeOffset.MinValue) || p.Start <= keyDate.UtcDateTime) && p.Patch != null)
                     .Where(p => p.Patch!.RootElement.ValueKind != System.Text.Json.JsonValueKind.Null)
                     .OrderBy(p => p.Start))

        {
            var jtokenPatch = JsonConvert.DeserializeObject<JToken>(existingPatch.Patch!.RootElement.GetRawText());
            left = jdp.Patch(left, jtokenPatch);
        }
        return _deserializer(JsonConvert.SerializeObject(left));
    }
}
