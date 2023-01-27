using Itenso.TimePeriod;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChronoJsonDiffPatch;

/// <summary>
/// Assume
/// * There are two key dates: dtA and dtB
/// * dtB &gt; dtA
/// * There is a patchA at dtA that puts our entity in state A.
/// * There is a patchB at dtB that puts our entity in state B. 
/// If the patching order is now: (patchB, patchA), what is the desired state at dtB?
/// This enums allows you to distinguish the behaviour of <see cref="TimeRangePatchChain{TEntity}.Add(TEntity,TEntity,System.DateTimeOffset)"/>
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

    /// <summary>
    /// initialize the collection by providing a list of time periods
    /// </summary>
    /// <param name="timeperiods"></param>
    /// <param name="serializer">a function that is able to serialize <typeparamref name="TEntity"/></param>
    /// <param name="deserializer">a function that is able to deserialize <typeparamref name="TEntity"/></param>
    public TimeRangePatchChain(IEnumerable<TimeRangePatch>? timeperiods = null, Func<TEntity, string>? serializer = null, Func<string, TEntity>? deserializer = null) : base(timeperiods ?? new List<TimeRangePatch> { })
    {
        _serializer = serializer ?? DefaultSerializer;
        _deserializer = deserializer ?? DefaultDeSerializer;
    }

    /// <summary>
    /// returns all Items ever added
    /// </summary>
    /// <returns></returns>
    public IEnumerable<TimeRangePatch> GetAll()
    {
        return this.Cast<TimeRangePatch>();
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

    public void Add(TEntity initialEntity, TEntity changedEntity, DateTimeOffset moment, FuturePatchBehaviour? futurePatchBehaviour = null)
    {
        if (Contains(moment))
        {
            throw new ArgumentException("You must not add something that already exists");
        }
        var jdp = new JsonDiffPatch();
        JToken patch;
        var changedToken = JToken.Parse(_serializer(changedEntity));
        if (Count == 0) // seems like this entity has no tracked changes so far
        {
            var initialToken = JToken.Parse(_serializer(initialEntity));
            changedToken = JToken.Parse(_serializer(changedEntity));
            // We initially add 2 patches:
            // 1) an empty patch from the beginning of time (-infinity) up to the moment at which the changes is applied
            //    this means: the entity is unchanged from -infinity up to the given moment.
            // 2) a patch that describes the changes that happen at the moment
            // their order is such that the index[0] of the list contains the most recent change and index[-1] starts at -infinity
            patch = jdp.Diff(initialToken, changedToken);
            base.Add(new TimeRangePatch(from: DateTimeOffset.MinValue.UtcDateTime, patch: null, to: moment.UtcDateTime));
            base.Add(new TimeRangePatch(from: moment, patch: System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(patch)), to: null));
            return;
        }
        var upToDateToken = JToken.Parse(_serializer(PatchToDate(initialEntity, moment)));
        patch = jdp.Diff(upToDateToken, changedToken);
        // there are already patches present
        // first add a patch that starts at the change moment and end at +infinity
        var fromMomentTillInfinity = new TimeRangePatch(from: moment, patch: System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(patch)), to: null);
        Action<TimeRangePatch> insertAction = (trp) => base.Add(trp); // we defer the insert until we're done with looping over the collection
        var index = -1;
        foreach (var existingPatch in GetAll())
        {
            index += 1; // starts at 0 but is always incremented at begin of loop
            if (!existingPatch.OverlapsWith(fromMomentTillInfinity))
            {
                continue;
            }
            var intersection = existingPatch.GetIntersection(fromMomentTillInfinity);
            // we remove the intersection from the existing patch
            if (intersection.Start > existingPatch.Start)
            {
                existingPatch.ShrinkEndTo(intersection.Start);
                insertAction = (trp) => base.Add(trp); // append at end of list
            }
            else if (intersection.End > existingPatch.Start)
            {
                if (!futurePatchBehaviour.HasValue)
                {
                    throw new ArgumentNullException(nameof(futurePatchBehaviour));
                }

                if (futurePatchBehaviour == FuturePatchBehaviour.KeepTheFuture)
                {
                    fromMomentTillInfinity.ShrinkEndTo(intersection.Start);
                    var previousPatch = (TimeRangePatch)this[index - 1];
                    previousPatch.ShrinkEndTo(fromMomentTillInfinity.Start);
                    existingPatch.Move(-fromMomentTillInfinity.Duration); // this is a preparation for the following insert action
                    insertAction = (trp) => base.Insert(index, trp);
                    // not only do we have to add the patch at this index later but also we need to modify the existing patch because its predecessor changed
                    var futureToken = JToken.Parse(_serializer(PatchToDate(initialEntity, intersection.Start)));
                    var updatedPatch = new JsonDiffPatch().Diff(changedToken, futureToken);
                    existingPatch.Patch = System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(updatedPatch));
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        insertAction(fromMomentTillInfinity);
    }

    public TEntity PatchToDate(TEntity initialEntity, DateTimeOffset keyDate)
    {
        var jdp = new JsonDiffPatchDotNet.JsonDiffPatch();
        var left = JToken.Parse(_serializer(initialEntity));

        foreach (var existingPatch in this.GetAll()
                     .Where(p => ((p.Start == DateTime.MinValue && keyDate != DateTimeOffset.MinValue) || p.Start <= keyDate.UtcDateTime) && p.Patch != null)
                     .Where(p => p.Patch!.RootElement.ValueKind != System.Text.Json.JsonValueKind.Null)
                     .OrderBy(p => p.Start))

        {
            var jtokenPatch = JsonConvert.DeserializeObject<JToken>(existingPatch.Patch.RootElement.GetRawText());
            left = jdp.Patch(left, jtokenPatch);
        }
        return _deserializer(JsonConvert.SerializeObject(left));
    }
}
