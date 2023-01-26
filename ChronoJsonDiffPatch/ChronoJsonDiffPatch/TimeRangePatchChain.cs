﻿using Itenso.TimePeriod;
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

    public void Add(TEntity initialEntity, TEntity changedEntity, DateTimeOffset moment)
    {
        if (Contains(moment))
        {
            throw new ArgumentException("You must not add something that already exists");
        }
        var jdp = new JsonDiffPatchDotNet.JsonDiffPatch();
        var initialToken = JToken.Parse(_serializer(initialEntity));
        var changedToken = JToken.Parse(_serializer(changedEntity));
        JToken patch = jdp.Diff(changedToken, initialToken);
        if (Count == 0) // seems like this entity has no tracked changes so far
        {
            // We initially add 2 patches:
            // 1) an empty patch from the beginning of time (-infinity) up to the moment at which the changes is applied
            //    this means: the entity is unchanged from -infinity up to the given moment.
            // 2) a patch that describes the changes that happen at the moment
            // their order is such that the index[0] of the list contains the most recent change and index[-1] starts at -infinity

            base.Add(new TimeRangePatch(from: DateTimeOffset.MinValue.UtcDateTime, System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(patch)),
                to: moment.UtcDateTime));
            base.Add(new TimeRangePatch(from: moment, null));
            return;
        }

        // there are already patches present
        // first add a patch that starts at the change moment and end at +infinity
        var fromMomentTillInfinity = new TimeRangePatch(from: moment, null);
        var overlappingPatches = this.GetAll().Where(ep => ep.OverlapsWith(fromMomentTillInfinity));
        foreach (var existingPatch in overlappingPatches)
        {
            var intersection = existingPatch.GetIntersection(fromMomentTillInfinity);
            // we remove the intersection from the existing patch
            if (intersection.Start > existingPatch.Start)
            {
                existingPatch.ShrinkEndTo(intersection.Start);
            }
            else if (intersection.End > existingPatch.Start)
            {
                existingPatch.ShrinkStartTo(intersection.End);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }

    public TEntity PatchToDate(TEntity initialEntity, DateTimeOffset keyDate)
    {
        var jdp = new JsonDiffPatchDotNet.JsonDiffPatch();
        var left = JToken.Parse(_serializer(initialEntity));

        foreach (var existingPatch in this.GetAll().OrderByDescending(p => p.Start).Where(p => ((p.Start == DateTime.MinValue && keyDate != DateTimeOffset.MinValue) || p.Start <= keyDate.UtcDateTime) && p.Patch != null))
        {
            if (existingPatch.Patch!.RootElement.ValueKind != System.Text.Json.JsonValueKind.Null)
            {
                var jtokenPatch = JsonConvert.DeserializeObject<JToken>(existingPatch.Patch.RootElement.GetRawText());
                left = jdp.Patch(left, jtokenPatch);
            }
        }
        return _deserializer(JsonConvert.SerializeObject(left));
    }
}
