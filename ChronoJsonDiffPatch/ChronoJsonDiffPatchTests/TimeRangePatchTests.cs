using System.Text.Json;
using ChronoJsonDiffPatch;
using FluentAssertions;

namespace ChronoJsonDiffPatchTests;

public class TimeRangePatchTests
{
    public enum Serializer
    {
        SystemText,
        Newtonsoft,
    }

    private static T SerializationRoundtrip<T>(T candidate, Serializer serializer, Serializer deSerializer)
    {
        var jsonString = serializer switch
        {
            Serializer.SystemText => System.Text.Json.JsonSerializer.Serialize(candidate),
            Serializer.Newtonsoft => Newtonsoft.Json.JsonConvert.SerializeObject(candidate),
            _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null)
        };
        jsonString.Should().NotBe(null);
        var deserialized = deSerializer switch
        {
            Serializer.SystemText => System.Text.Json.JsonSerializer.Deserialize<T>(jsonString),
            Serializer.Newtonsoft => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonString),
            _ => throw new ArgumentOutOfRangeException(nameof(deSerializer), deSerializer, null)
        } ?? throw new InvalidOperationException();
        deserialized.Should().BeEquivalentTo(candidate);
        return deserialized;
    }

    [Theory]
    [InlineData(Serializer.SystemText, Serializer.SystemText)]
    [InlineData(Serializer.SystemText, Serializer.Newtonsoft)]
    [InlineData(Serializer.Newtonsoft, Serializer.SystemText)]
    [InlineData(Serializer.Newtonsoft, Serializer.Newtonsoft)]
    public void Test_Serialization(Serializer serializer, Serializer deserializer)
    {
        var emptyPatch = new TimeRangePatch();
        SerializationRoundtrip(emptyPatch, serializer, deserializer);
    }

    [Fact]
    public void Test_ToString()
    {
        var patch = JsonDocument.Parse("""{"foo": "bar"}""");
        var emptyPatch = new TimeRangePatch(patch: patch, from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero), to: new DateTimeOffset(2023, 3, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var actual = emptyPatch.ToString();
        actual.Should().Be("[2022-01-01T00:00:00.0000000+00:00, 2023-03-02T01:00:00.0000000+00:00): {\"foo\": \"bar\"}");
    }
}
