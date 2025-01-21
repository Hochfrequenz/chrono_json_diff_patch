using System.Text.Json;
using ChronoJsonDiffPatch;
using FluentAssertions;
using Itenso.TimePeriod;

namespace ChronoJsonDiffPatchTests;

public class TimeRangePatchTests
{
    public enum Serializer
    {
        SystemText,
        Newtonsoft,
    }

    private static T SerializationRoundtrip<T>(
        T candidate,
        Serializer serializer,
        Serializer deSerializer
    )
    {
        var jsonString = serializer switch
        {
            Serializer.SystemText => JsonSerializer.Serialize(candidate),
            Serializer.Newtonsoft => Newtonsoft.Json.JsonConvert.SerializeObject(candidate),
            _ => throw new ArgumentOutOfRangeException(nameof(serializer), serializer, null),
        };
        jsonString.Should().NotBe(null);
        var deserialized =
            deSerializer switch
            {
                Serializer.SystemText => JsonSerializer.Deserialize<T>(jsonString),
                Serializer.Newtonsoft => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(
                    jsonString
                ),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(deSerializer),
                    deSerializer,
                    null
                ),
            } ?? throw new InvalidOperationException();
        deserialized.Should().BeEquivalentTo(candidate);
        return deserialized;
    }

    [Theory]
    [InlineData(Serializer.SystemText, Serializer.SystemText)]
    [InlineData(Serializer.SystemText, Serializer.Newtonsoft)]
    [InlineData(Serializer.Newtonsoft, Serializer.SystemText)]
    [InlineData(Serializer.Newtonsoft, Serializer.Newtonsoft)]
    public void Test_Serialization_EmptyPatch(Serializer serializer, Serializer deserializer)
    {
        var emptyPatch = new TimeRangePatch();
        SerializationRoundtrip(emptyPatch, serializer, deserializer);
    }

    [Theory]
    [InlineData(Serializer.SystemText, Serializer.SystemText)]
    [InlineData(Serializer.SystemText, Serializer.Newtonsoft)]
    [InlineData(Serializer.Newtonsoft, Serializer.SystemText)]
    [InlineData(Serializer.Newtonsoft, Serializer.Newtonsoft)]
    public void Test_Serialization_OpenPatch(Serializer serializer, Serializer deserializer)
    {
        var openPatch = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        SerializationRoundtrip(openPatch, serializer, deserializer);
    }

    [Theory]
    [InlineData(Serializer.SystemText, Serializer.SystemText)]
    [InlineData(Serializer.SystemText, Serializer.Newtonsoft)]
    [InlineData(Serializer.Newtonsoft, Serializer.SystemText)]
    [InlineData(Serializer.Newtonsoft, Serializer.Newtonsoft)]
    public void Test_Serialization_ClosedPatch(Serializer serializer, Serializer deserializer)
    {
        var closedPatch = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        SerializationRoundtrip(closedPatch, serializer, deserializer);
    }

    [Fact]
    public void Test_ToString()
    {
        var patch = JsonDocument.Parse("""{"foo": "bar"}""");
        var trp = new TimeRangePatch(
            patch: patch,
            from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2023, 3, 2, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var actual = trp.ToString();
        actual
            .Should()
            .Be(
                "[2022-01-01T00:00:00.0000000+00:00, 2023-03-02T01:00:00.0000000+00:00): {\"foo\": \"bar\"}"
            );
    }

    [Fact]
    public void Test_Start_Property()
    {
        ITimeRange trp = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var actual = trp.Start;
        actual.Should().Be(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcDateTime);
    }

    [Fact]
    public void Test_End_Property()
    {
        ITimeRange trp = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2023, 3, 2, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var actual = trp.End;
        actual.Should().Be(new DateTimeOffset(2023, 3, 2, 1, 0, 0, 0, TimeSpan.Zero).UtcDateTime);
    }

    [Fact]
    public void Test_End_Property_Lower_than_Start()
    {
        Action instantiation = () =>
        {
            _ = new TimeRangePatch(
                patch: null,
                from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
                to: new DateTimeOffset(2021, 3, 2, 1, 0, 0, 0, TimeSpan.Zero)
            );
        };
        instantiation.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Test_End_Property_Open()
    {
        ITimeRange trp = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            to: null
        );
        var actual = trp.End;
        actual.Should().Be(DateTime.MaxValue);
    }
}
