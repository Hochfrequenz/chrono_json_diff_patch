using ChronoJsonDiffPatch;
using FluentAssertions;

namespace ChronoJsonDiffPatchTests;

public class TimeRangePatchSerializationTests
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
}
