using System.Diagnostics.Contracts;
using Itenso.TimePeriod;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChronoJsonDiffPatch;

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

    private readonly Func<TEntity, string> _serialize;
    private readonly Func<string, TEntity> _deserialize;
    private readonly IEnumerable<ISkipCondition<TEntity>>? _skipConditions;
    private List<TimeRangePatch> _skippedPatches = new();

    /// <summary>
    /// patches that have been skipped because of errors
    /// </summary>
    public IReadOnlyList<TimeRangePatch> SkippedPatches
    {
        get => _skippedPatches.AsReadOnly();
    }

    /// <summary>
    /// set to true, if, while modifying the chain any of the skip conditions provided to the construct were used.
    /// </summary>
    public bool PatchesHaveBeenSkipped
    {
        get => SkippedPatches?.Any() == true;
    }

    /// <summary>
    /// converts the given <paramref name="entity"/> to an JToken using the serializer configured in the constructor (or default)
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    protected JToken ToJToken(TEntity entity)
    {
        return JToken.Parse(_serialize(entity));
    }

    /// <summary>
    /// converts the given <paramref name="jsonDiffPatch"/> to a <see cref="System.Text.Json.JsonDocument"/> using (default) Newtonsoft
    /// </summary>
    /// <param name="jsonDiffPatch"></param>
    /// <returns></returns>
    /// <remarks>I don't know why it uses Newtonsoft; It's just like the MWE of JsonDiffPatch</remarks>
    protected System.Text.Json.JsonDocument ToJsonDocument(JToken jsonDiffPatch)
    {
        return System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(jsonDiffPatch));
    }

    /// <summary>
    /// The patching direction describes how the patches are sorted; See <see cref="PatchingDirection"/>
    /// It's a readonly property, because changing the property does require changing the data as well.
    /// </summary>
    public PatchingDirection PatchingDirection { get; private set; }

    /// <summary>
    /// prepare the given (optional) time periods to forward them to the base constructor
    /// </summary>
    /// <param name="timeperiods"></param>
    /// <returns></returns>
    private static IEnumerable<TimeRangePatch> PrepareForTimePeriodChainConstructor(
        IEnumerable<TimeRangePatch>? timeperiods
    )
    {
        if (timeperiods is null)
        {
            return new List<TimeRangePatch>();
        }

        var result = timeperiods.OrderBy(tp => tp.Start);
        var ambigousStarts = result
            .GroupBy(tp => tp.Start)
            .Where(g => g.Count() > 1)
            .Select(g => g.Select(x => x.Start.ToString("o")).First())
            .Distinct();
        var ambigousEnds = result
            .GroupBy(tp => tp.End)
            .Where(g => g.Count() > 1)
            .Select(g => g.Select(x => x.End.ToString("o")).First())
            .Distinct();
        ;
        bool baseConstructorIsLikelyToCrash = ambigousStarts.Any() || ambigousEnds.Any();
        if (baseConstructorIsLikelyToCrash)
        {
            try
            {
                _ = new TimePeriodChain(result); // a test if the base constructor will actually crash?
            }
            catch (InvalidOperationException invalidOpException)
                when (invalidOpException.Message.EndsWith("out of range"))
            {
                // if it would crash and we do know the reasons, then we throw a more meaningful exception here instead of waiting for the base class to crash
                throw new ArgumentException(
                    $"The given periods contain ambiguous starts ({string.Join(", ", ambigousStarts)}) or ends ({string.Join(", ", ambigousEnds)})",
                    innerException: invalidOpException
                );
            }
        }

        return result;
    }

    /// <summary>
    /// initialize the collection by providing a list of time periods
    /// </summary>
    /// <param name="timeperiods"></param>
    /// <param name="patchingDirection">the direction in which the patches are to be applied; <see cref="PatchingDirection"/></param>
    /// <param name="serializer">a function that is able to serialize <typeparamref name="TEntity"/></param>
    /// <param name="deserializer">a function that is able to deserialize <typeparamref name="TEntity"/></param>
    /// <param name="skipConditions">optional conditions under which we allow a patch to fail and ignore its changes</param>
    public TimeRangePatchChain(
        IEnumerable<TimeRangePatch>? timeperiods = null,
        PatchingDirection patchingDirection = PatchingDirection.ParallelWithTime,
        Func<TEntity, string>? serializer = null,
        Func<string, TEntity>? deserializer = null,
        IEnumerable<ISkipCondition<TEntity>>? skipConditions = null
    )
        : base(PrepareForTimePeriodChainConstructor(timeperiods))
    {
        _serialize = serializer ?? DefaultSerializer;
        _deserialize = deserializer ?? DefaultDeSerializer;
        PatchingDirection = patchingDirection;
        if (
            timeperiods?.FirstOrDefault(tpr => tpr.PatchingDirection != PatchingDirection) is
            { } patchWithWrongDirection
        )
        {
            throw new ArgumentException(
                $"You must not add a patch {patchWithWrongDirection} with direction {patchWithWrongDirection.PatchingDirection}!={PatchingDirection}",
                nameof(timeperiods)
            );
        }

        _skipConditions = skipConditions;
    }

    /// <summary>
    /// returns all Items ever added
    /// </summary>
    /// <returns></returns>
    [Pure]
    public IEnumerable<TimeRangePatch> GetAll()
    {
        var result = this.Cast<TimeRangePatch>();
        var distinctPatchingDirections = result.DistinctBy(trp => trp.PatchingDirection);
        if (distinctPatchingDirections.Count() > 1)
        {
            throw new InvalidDataException(
                $"The chain is inconsistent; There are > 1 patching directions: {distinctPatchingDirections}"
            );
        }

        if (
            distinctPatchingDirections.Count() == 1
            && distinctPatchingDirections.Single().PatchingDirection != PatchingDirection
        )
        {
            throw new InvalidDataException(
                $"The chain is inconsistent: The chain patching direction {PatchingDirection} differs from the chains single elements directions {distinctPatchingDirections.Single().PatchingDirection}"
            );
        }

        return PatchingDirection switch
        {
            PatchingDirection.ParallelWithTime => result
                .OrderBy(trp => trp.From)
                .ThenBy(trp => trp.End),
            PatchingDirection.AntiParallelWithTime => result
                .OrderByDescending(trp => trp.From)
                .ThenByDescending(trp => trp.End),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    /// <summary>
    /// Returns true iff the chain contains an item that has a <see cref="TimeRange.Start"/> date whose start doesn't differ more than <paramref name="graceTicks"/> from <paramref name="start"/>.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="graceTicks"></param>
    /// <returns></returns>
    [Pure]
    public bool Contains(DateTimeOffset start, uint graceTicks = 1000)
    {
        if (graceTicks == 0)
        {
            return GetAll().Any(ze => ze.Start.ToUniversalTime() == start.UtcDateTime);
        }

        // the key times somehow differ by <1000 Ticks sometimes, this is probably because of ORM internals
        return GetAll()
            .Any(ze =>
                Math.Abs((ze.Start.ToUniversalTime() - start.UtcDateTime).Ticks) <= graceTicks
            );
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
    public void Add(
        TEntity initialEntity,
        TEntity changedEntity,
        DateTimeOffset moment,
        FuturePatchBehaviour? futurePatchBehaviour = null
    )
    {
        if (PatchingDirection == PatchingDirection.AntiParallelWithTime)
        {
            throw new NotImplementedException(
                $"Adding patches to a chain with {nameof(PatchingDirection)}=={PatchingDirection} is not implemented yet. Please reverse the chain and try again"
            );
        }

        var jdp = new JsonDiffPatch();
        JToken patchAtKeyDate;
        var changedToken = ToJToken(changedEntity);
        if (Count == 0) // seems like this entity has no tracked changes so far
        {
            var initialToken = ToJToken(initialEntity);
            // We initially add 2 patches (whose content and start/end depends on the PatchingDirection)
            // 1) an empty patch from the beginning of time (-infinity) up to the moment at which the changes is applied
            //    this means: the entity is unchanged from -infinity up to the given moment.
            var trpFromBeginningOfTimeUntilKeyDate = new TimeRangePatch(
                from: DateTimeOffset.MinValue,
                patch: null,
                to: moment.UtcDateTime,
                patchingDirection: PatchingDirection
            );
            base.Add(trpFromBeginningOfTimeUntilKeyDate);
            // 2) a patch that describes the changes that happen at the moment
            // their order is such that the index[0] of the list contains the most recent change and index[-1] starts at -infinity
            patchAtKeyDate = jdp.Diff(initialToken, changedToken);
            var trpSinceKeyDate = new TimeRangePatch(
                from: moment.UtcDateTime,
                patch: ToJsonDocument(patchAtKeyDate),
                to: null,
                patchingDirection: PatchingDirection
            );
            base.Add(trpSinceKeyDate);
            return;
        }

        var upToDateToken = ToJToken(PatchToDate(initialEntity, moment));
        patchAtKeyDate = jdp.Diff(upToDateToken, changedToken);
        // there are already patches present
        // first add a patch that starts at the change moment and end at +infinity
        var patchToBeAdded = new TimeRangePatch(
            from: moment.UtcDateTime,
            patch: ToJsonDocument(patchAtKeyDate),
            to: null,
            patchingDirection: PatchingDirection
        );

        bool anyExistingSliceStartsLater = GetAll().Any(trp => trp.Start > patchToBeAdded.Start);
        if (anyExistingSliceStartsLater)
        {
            switch (futurePatchBehaviour)
            {
                case null:
                    throw new ArgumentNullException(
                        nameof(futurePatchBehaviour),
                        $"There already exists any patch whose TimeRange.Start is &gt;= {moment}! You have to specify how the requested \"change in the past\" shall behave"
                    );
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

        foreach (var infinitelyNarrowPatch in GetAll().Where(trp => trp.IsMoment))
        {
            if (GetAll().Any(trp => trp.End == infinitelyNarrowPatch.Start && !trp.IsMoment)) // join the patch which is going to be deleted with another patch if it exists
            {
                var artificialChainWithoutTheRecentlyAddedPatch = new TimeRangePatchChain<TEntity>(
                    GetAll().Where(p => !p.Equals(patchToBeAdded)),
                    skipConditions: _skipConditions
                );
                var stateWithoutMomentPatch =
                    artificialChainWithoutTheRecentlyAddedPatch.PatchToDate(
                        initialEntity,
                        infinitelyNarrowPatch.Start
                    );
                var jsonStateWithoutMomentPatch = ToJToken(stateWithoutMomentPatch);
                var jdpDelta = new JsonDiffPatch();
                var jsonStateWithMomentPatch = jdpDelta.Patch(
                    jsonStateWithoutMomentPatch,
                    JsonConvert.DeserializeObject<JToken>(
                        patchToBeAdded.Patch!.RootElement.GetRawText()
                    )
                );
                var jsonStateBeforeMomentPatch = ToJToken(
                    artificialChainWithoutTheRecentlyAddedPatch.PatchToDate(
                        initialEntity,
                        infinitelyNarrowPatch.Start - TimeSpan.FromTicks(1)
                    )
                );
                var jdpDelta2 = new JsonDiffPatch();
                var result = jdpDelta2.Diff(jsonStateBeforeMomentPatch, jsonStateWithMomentPatch);
                patchToBeAdded.Patch = ToJsonDocument(result);
            }

            Remove(infinitelyNarrowPatch);
        }
    }

    private void AddAndKeepTheFuture(
        TEntity initialEntity,
        TimeRangePatch patchToBeAdded,
        JToken changedToken,
        DateTimeOffset moment
    )
    {
        if (Contains(moment))
        {
            var entriesWhosePatchShouldBeReplaced = GetAll()
                .Where(trp => trp.Start >= moment.UtcDateTime);
            var entryWhosePatchShouldBeReplaced = entriesWhosePatchShouldBeReplaced.Single(trp =>
                trp.Start == moment.UtcDateTime
            );
            // The entryWhosePatchShouldBeReplaced.Patch can only be replaced with patchToBeAdded.Patch if ALL properties of the old and the replaced patch are changed
            // In general the new patch should describe the difference to the state just before the keydate; That's a subtle difference.
            if (moment.UtcDateTime != DateTimeOffset.MinValue.UtcDateTime)
            {
                var stateJustBeforeThePatch = ToJToken(
                    PatchToDate(initialEntity, moment.UtcDateTime - TimeSpan.FromTicks(1))
                );
                var patchAtKeyDate = ToJsonDocument(
                    new JsonDiffPatch().Diff(stateJustBeforeThePatch, changedToken)
                );
                patchToBeAdded.Patch = patchAtKeyDate;
            }

            entryWhosePatchShouldBeReplaced.Patch = patchToBeAdded.Patch;

            // we also need to modify the following entry.
            // If the original patches are [A,B],[B,C],[C,D] and we replace B with X, then we not only have to replace [A,B] with [A,X] (which we already did in the entryWhosePatchShouldBeReplaced)
            // but also replace [B,C] with [X,C].
            if (
                GetAll().SingleOrDefault(trp => trp.Start == entryWhosePatchShouldBeReplaced.End) is
                { } followingEntry
            )
            {
                var tokenBeginningOfNextSlice = ToJToken(
                    PatchToDate(initialEntity, followingEntry.Start.ToUniversalTime())
                );
                var updatedFollowingPatch = new JsonDiffPatch().Diff(
                    changedToken,
                    tokenBeginningOfNextSlice
                );
                followingEntry.Patch = ToJsonDocument(updatedFollowingPatch);
            }
        }
        else
        {
            var indexAtWhichThePatchShallBeAdded =
                IndexOf(GetAll().Last(trp => trp.Start.ToUniversalTime() <= moment.UtcDateTime))
                + 1;

            // We shrink the patch to be added such that it does not overwrite existing future patches ("Keep the Future").
            // So we make it end where the next patch starts.
            var firstExistingOverlappingPatch = GetAll()
                .First(trp => trp.Start > patchToBeAdded.Start);
            var intersection = firstExistingOverlappingPatch.GetIntersection(patchToBeAdded);
            patchToBeAdded.ShrinkEndTo(new DateTimeOffset(intersection.Start).UtcDateTime);

            //we need to modify the existing patch itself because its predecessor changed
            var tokenAtIntersectionStart = ToJToken(
                PatchToDate(initialEntity, intersection.Start.ToUniversalTime())
            );
            var updatedPatch = new JsonDiffPatch().Diff(changedToken, tokenAtIntersectionStart);
            firstExistingOverlappingPatch.Patch = ToJsonDocument(updatedPatch);

            var subtractor = new TimePeriodSubtractor<TimeRangePatch>();
            ITimePeriodCollection existingPatchesWithoutTheRangeCoveredByThePatchToBeAdded =
                subtractor.SubtractPeriods(this, new TimePeriodCollection { patchToBeAdded });
            if (existingPatchesWithoutTheRangeCoveredByThePatchToBeAdded.Count == Count)
            {
                var n = -1;
                foreach (var item in existingPatchesWithoutTheRangeCoveredByThePatchToBeAdded)
                {
                    n += 1;
                    ((TimeRangePatch)this[n]).ShrinkTo(item); // shrink them all such there is exactly patchToBeAdded.Duration space left (somewhere in the middle)
                }
            }
            else
            {
                var itemsLeftOfTheGap =
                    existingPatchesWithoutTheRangeCoveredByThePatchToBeAdded.Where(p =>
                        p.Start < patchToBeAdded.Start
                    );
                bool anythingHasBeenShrunk = false;
                foreach (var itemLeftOfTheGap in itemsLeftOfTheGap)
                {
                    if (
                        GetAll()
                            .SingleOrDefault(p =>
                                p.Start == itemLeftOfTheGap.Start && p.End >= itemLeftOfTheGap.End
                            ) is
                        { } aPatch
                    )
                    {
                        aPatch.ShrinkTo(itemLeftOfTheGap);
                        anythingHasBeenShrunk = true;
                    }
                }

                if (!anythingHasBeenShrunk)
                {
                    GetAll()
                        .Last(p => p.Start < patchToBeAdded.Start)
                        .ShrinkEndTo(
                            itemsLeftOfTheGap.First(p => p.End >= patchToBeAdded.Start).End
                        );
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
                ((TimeRangePatch)First).ShrinkStartTo(
                    (DateTimeOffset.MinValue + patchToBeAdded.Duration).UtcDateTime
                );
            }

            Insert(indexAtWhichThePatchShallBeAdded, patchToBeAdded);
            if (startHasToBeShifted)
            {
                // don't ask. it passes the tests. that's all I wished for today
                var itemsLeftOfTheAddedPatch = this.Where(p =>
                        p.Start < patchToBeAdded.Start && p.End != patchToBeAdded.Start
                    )
                    .Cast<TimeRangePatch>();
                foreach (var itemLeftOfTheAddedPatch in itemsLeftOfTheAddedPatch)
                {
                    // OK... someone explain me this behaviour:
                    // If, before the insert, I move the items _after_ the keydate to the left,
                    // then, after the insert I have to move items _before_ the keydate to the right.
                    // I think this line just fixes symptoms. The causes are elsewhere.
                    // this is purely testdriven... maybe tobias is right and we should just write the timeperiod code by ourselfs ;)
                    if (
                        itemLeftOfTheAddedPatch.Start != DateTimeOffset.MinValue.UtcDateTime
                        && itemLeftOfTheAddedPatch.Start - patchToBeAdded.Duration
                            == DateTimeOffset.MinValue.UtcDateTime
                    )
                    {
                        itemLeftOfTheAddedPatch.Move(-patchToBeAdded.Duration);
                    }
                    else
                    {
                        itemLeftOfTheAddedPatch.Move(patchToBeAdded.Duration);
                    }
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
    }

    private void AddAndOverrideTheFuture(TimeRangePatch patchToBeAdded)
    {
        var futureOverlappingPatchIndexes = GetAll()
            .Where(trp => trp.Start >= patchToBeAdded.Start)
            .Select(IndexOf);
        foreach (var futureIndex in futureOverlappingPatchIndexes.Reverse())
        {
            RemoveAt(futureIndex);
        }

        if (
            GetAll().LastOrDefault(p => p.OverlapsWith(patchToBeAdded)) is
            { } lastOverlappingPatchWhichIsNotDeleted
        )
        {
            lastOverlappingPatchWhichIsNotDeleted.ShrinkEndTo(patchToBeAdded.Start);
        }

        Add(patchToBeAdded);
    }

    /// <summary>
    /// start at <paramref name="initialEntity"/> at beginning of time.
    /// Then apply all the patches up to <paramref name="keyDate"/> and return the state of the entity at <paramref name="keyDate"/>
    /// </summary>
    /// <param name="initialEntity">the state of <typeparamref name="TEntity"/> at the beginning of time</param>
    /// <param name="keyDate">the date up to which you'd like to apply the patches</param>
    /// <returns>the state of the entity after all the patches up to <paramref name="keyDate"/> have been applied</returns>
    [Pure]
    public TEntity PatchToDate(TEntity initialEntity, DateTimeOffset keyDate)
    {
        var jdp = new JsonDiffPatch();
        var left = ToJToken(initialEntity);
        _skippedPatches = new();
        switch (PatchingDirection)
        {
            case PatchingDirection.ParallelWithTime:
            {
                var index = -1;

                foreach (
                    var existingPatch in GetAll()
                        .Where(p =>
                            (
                                (p.Start == DateTime.MinValue && keyDate != DateTimeOffset.MinValue)
                                || p.Start <= keyDate.UtcDateTime
                            )
                            && p.Patch != null
                        )
                        .Where(p =>
                            p.Patch!.RootElement.ValueKind != System.Text.Json.JsonValueKind.Null
                        )
                )
                {
                    index += 1;
                    var jtokenPatch = JsonConvert.DeserializeObject<JToken>(
                        existingPatch.Patch!.RootElement.GetRawText()
                    );
                    try
                    {
                        left = jdp.Patch(left, jtokenPatch);
                    }
                    catch (Exception exc)
                    {
                        var entityBeforePatch = _deserialize(left.ToString());

                        if (
                            _skipConditions?.Any(sc =>
                                sc.ShouldSkipPatch(entityBeforePatch, existingPatch, exc)
                            ) == true
                        )
                        {
                            _skippedPatches.Add(existingPatch);
                            continue;
                        }

                        throw new PatchingException<TEntity>(
                            stateOfEntityBeforeAnyPatch: initialEntity,
                            left: left,
                            patch: jtokenPatch,
                            index: index,
                            message: $"Failed to apply patches ({PatchingDirection}): {exc.Message}; None of the {_skipConditions?.Count() ?? 0} skip conditions applied",
                            innerException: exc
                        );
                    }
                }

                return _deserialize(JsonConvert.SerializeObject(left));
            }
            case PatchingDirection.AntiParallelWithTime:
            {
                var index = 0;
                foreach (
                    var existingPatch in GetAll()
                        .Where(p => p.End > keyDate)
                        .Where(p =>
                            p.Patch != null
                            && p.Patch!.RootElement.ValueKind != System.Text.Json.JsonValueKind.Null
                        )
                )
                {
                    index += 1;
                    var jtokenPatch = JsonConvert.DeserializeObject<JToken>(
                        existingPatch.Patch!.RootElement.GetRawText()
                    );
                    try
                    {
                        left = jdp.Unpatch(left, jtokenPatch);
                    }
                    catch (Exception exc)
                    {
                        var entityBeforePatch = _deserialize(left.ToString());
                        if (
                            _skipConditions?.Any(sc =>
                                sc.ShouldSkipPatch(entityBeforePatch, existingPatch, exc)
                            ) == true
                        )
                        {
                            _skippedPatches.Add(existingPatch);
                            continue;
                        }

                        throw new PatchingException<TEntity>(
                            stateOfEntityBeforeAnyPatch: initialEntity,
                            left: left,
                            patch: jtokenPatch,
                            index: index,
                            message: $"Failed to apply patches ({PatchingDirection}): {exc.Message}; None of the {_skipConditions?.Count() ?? 0} skip conditions applied",
                            innerException: exc
                        );
                    }
                }

                return _deserialize(JsonConvert.SerializeObject(left));
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// This method returns a reversed chain for the same data.
    /// Reversing means, that the key date of the base entity (its initial state) switches its sign (+/- infinity).
    /// Also the patches are reversed such that they have to be read right to left instead of left to right or vice versa respectively.
    /// Technically this changes the chains <see cref="PatchingDirection"/> from <see cref="ChronoJsonDiffPatch.PatchingDirection.ParallelWithTime"/> to <see cref="ChronoJsonDiffPatch.PatchingDirection.AntiParallelWithTime"/> or vice versa
    /// and also changes the <see cref="TimeRangePatch.PatchingDirection"/> of each patch in the chain.
    ///
    /// </summary>
    /// <returns>
    /// There are two cases to consider:
    ///
    /// 1) Assuming that this chain has <see cref="ChronoJsonDiffPatch.PatchingDirection.ParallelWithTime"/>, then, if you provide the state of the entity at -infinity (<paramref name="initialEntity"/>),
    /// this method returns a tuple of an <typeparamref name="TEntity"/> and a <see cref="TimeRangePatchChain{TEntity}"/> where the returned entity is the state of the entity at +infinity
    /// and the returned chain has PatchingDirection <see cref="ChronoJsonDiffPatch.PatchingDirection.AntiParallelWithTime"/>.
    ///
    /// 2) Assuming that this chain has <see cref="ChronoJsonDiffPatch.PatchingDirection.AntiParallelWithTime"/>, then, if you provide the state of the entity at +infinity (<paramref name="initialEntity"/>),
    /// this method returns a tuple of an <typeparamref name="TEntity"/> and a <see cref="TimeRangePatchChain{TEntity}"/> where the returned entity is the state of the entity at -infinity
    /// and the returned chain has PatchingDirection <see cref="ChronoJsonDiffPatch.PatchingDirection.ParallelWithTime"/>.
    ///
    /// In neither of the cases the state of this instance is modified (the method is pure).
    /// </returns>
    [Pure]
    public Tuple<TEntity, TimeRangePatchChain<TEntity>> Reverse(TEntity initialEntity)
    {
        switch (PatchingDirection)
        {
            case PatchingDirection.ParallelWithTime:
            {
                var stateAtPlusInfinity = PatchToDate(initialEntity, DateTimeOffset.MaxValue);
                List<TimeRangePatch> backwardsPatches = GetAll()
                    .OrderByDescending(trp => trp.End)
                    .Select(forwardPatch =>
                    {
                        var stateAfterPatchDate = PatchToDate(initialEntity, forwardPatch.Start);
                        var stateBeforePatchDate = PatchToDate(initialEntity, forwardPatch.End);
                        var backwardPatch = new JsonDiffPatch().Diff(
                            ToJToken(stateAfterPatchDate),
                            ToJToken(stateBeforePatchDate)
                        );
                        var backwardsTrp = new TimeRangePatch(
                            from: forwardPatch.Start,
                            to: forwardPatch.End,
                            patch: ToJsonDocument(backwardPatch),
                            patchingDirection: PatchingDirection.AntiParallelWithTime
                        );
                        return backwardsTrp;
                    })
                    .OrderBy(trp => trp.From)
                    .ToList();
                return new Tuple<TEntity, TimeRangePatchChain<TEntity>>(
                    stateAtPlusInfinity,
                    new TimeRangePatchChain<TEntity>(
                        backwardsPatches,
                        PatchingDirection.AntiParallelWithTime,
                        skipConditions: _skipConditions
                    )
                );
            }
            case PatchingDirection.AntiParallelWithTime:
            {
                var stateAtMinusInfinity = PatchToDate(initialEntity, DateTimeOffset.MinValue);
                List<TimeRangePatch> forwardPatches = GetAll()
                    .OrderBy(trp => trp.Start)
                    .Select(backwardsPatch =>
                    {
                        var stateAtPatchDate = PatchToDate(initialEntity, backwardsPatch.Start);
                        var forwardsPatch = new JsonDiffPatch().Diff(
                            ToJToken(stateAtMinusInfinity),
                            ToJToken(stateAtPatchDate)
                        );
                        var forwardsTrp = new TimeRangePatch(
                            from: backwardsPatch.Start,
                            to: backwardsPatch.End,
                            patch: ToJsonDocument(forwardsPatch),
                            patchingDirection: PatchingDirection.ParallelWithTime
                        );
                        return forwardsTrp;
                    })
                    .OrderBy(trp => trp.From)
                    .ToList();
                return new Tuple<TEntity, TimeRangePatchChain<TEntity>>(
                    stateAtMinusInfinity,
                    new TimeRangePatchChain<TEntity>(
                        forwardPatches,
                        PatchingDirection.ParallelWithTime,
                        skipConditions: _skipConditions
                    )
                );
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
