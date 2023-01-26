﻿using Itenso.TimePeriod;
using Newtonsoft.Json;

namespace ChronoJsonDiffPatch;

/// <summary>
/// A collection of <see cref="TimeRangePatch"/>es that implements <see cref="ITimePeriodChain"/>
/// </summary>
/// <remarks>We inherit from <see cref="TimePeriodChain"/> because this guarantees that there are no gaps between two <see cref="TimeRangePatch"/>es</remarks>
public class TimeRangePatchChain : TimePeriodChain
{
    private static readonly JsonSerializerSettings DefaultJsonSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        NullValueHandling = NullValueHandling.Ignore,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc
    };

    private JsonSerializerSettings _jsonSerializerSettings;

    /// <summary>
    /// initialize the collection by providing a list of time periods
    /// </summary>
    /// <param name="timeperiods"></param>
    /// <param name="jsonSerializerSettings"></param>
    public TimeRangePatchChain(IEnumerable<TimeRangePatch> timeperiods, JsonSerializerSettings? jsonSerializerSettings = null) : base(timeperiods)
    {
        _jsonSerializerSettings = jsonSerializerSettings ?? DefaultJsonSerializerSettings;
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
}
