using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using Itenso.TimePeriod;
using Newtonsoft.Json;

namespace ChronoJsonDiffPatch;

/// <summary>
/// A TimeRangePatch is a wrapper around a <see cref="JsonDocument"/> (which contains a <see cref="JsonDiffPatchDotNet.JsonDiffPatch"/>) that implements the <see cref="ITimeRange"/> interface.
/// </summary>
public class TimeRangePatch : TimeRange
{
    /// <summary>
    /// The <see cref="JsonDiffPatchDotNet.JsonDiffPatch"/> serialized as a <see cref="JsonDocument"/>
    /// </summary>
    [Column(TypeName = "jsonb")]
    [JsonPropertyName("patch")]
    [JsonPropertyOrder(10)]
    [JsonProperty(PropertyName = "patch", Order = 10, Required = Required.Default)]
    public JsonDocument? Patch { get; set; }

    /// <summary>
    /// inclusive begin/from datetime
    /// </summary>
    [Key]
    [JsonProperty(PropertyName = "from", Order = 11, Required = Required.Default)]
    [JsonPropertyName("from")]
    [JsonPropertyOrder(11)]
    public DateTimeOffset From
    {
        get => Start == DateTime.MinValue ? DateTimeOffset.MinValue : new DateTimeOffset(Start);
        set => Start = value == DateTimeOffset.MinValue ? DateTimeOffset.MinValue.UtcDateTime : value.UtcDateTime;
    }

    /// <summary>
    /// exclusive end/to datetime
    /// </summary>
    /// <remarks>may be null for open time slices</remarks>
    [JsonProperty(PropertyName = "to", Order = 12, Required = Required.AllowNull)]
    [JsonPropertyName("to")]
    [JsonPropertyOrder(12)]
    public DateTimeOffset? To
    {
        get => End == DateTime.MaxValue ? DateTimeOffset.MaxValue : new DateTimeOffset(End);
        set
        {
            if (value.HasValue)
            {
                if (value.Value < From)
                {
                    throw new ArgumentException($"{nameof(value)} ({value.Value:o}) must not be lower than {nameof(From)} ({From:o})");
                }
                End = value.Value.UtcDateTime;
            }
            else
            {
                End = DateTimeOffset.MaxValue.UtcDateTime;
            }
        }
    }

    /// <summary>
    /// The latest timestamp
    /// </summary>
    /// <remarks>Thought to be used when storing TimeRangePatches in a database</remarks>
    [Timestamp]
    [JsonPropertyName("timestamp")]
    [JsonPropertyOrder(13)]
    [JsonProperty(PropertyName = "timestamp", Order = 13)]
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// empty constructor (required for JSON deserialization)
    /// </summary>
    public TimeRangePatch()
    {
    }

    /// <summary>
    /// by default: assume the From-date is now
    /// </summary>
    /// <param name="patch"></param>
    public TimeRangePatch(JsonDocument? patch) : this(DateTimeOffset.UtcNow, patch)
    {
    }

    /// <summary>
    /// instantiate with given "from" date and open end
    /// </summary>
    public TimeRangePatch(DateTimeOffset from, JsonDocument? patch, DateTimeOffset? to = null)
    {
        To = to;
        From = from;
        Patch = patch;
    }

    /// <summary>
    /// a human readable representation of the time range
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        var toString = To.HasValue ? To.Value.ToString("o") : "?";
        return $"[{From:o}, {toString}): {Patch?.RootElement.ToString()}";
    }
}
