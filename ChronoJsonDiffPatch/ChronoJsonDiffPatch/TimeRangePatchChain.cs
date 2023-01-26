using Itenso.TimePeriod;
using Newtonsoft.Json;

namespace ChronoJsonDiffPatch;

/// <summary>
/// A collection of <see cref="TimeRangePatch"/>es that implements <see cref="ITimePeriodChain"/>
/// </summary>
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
