using System.Text.Json.Serialization;
using ChronoJsonDiffPatch;
using FluentAssertions;

namespace ChronoJsonDiffPatchTests;

public class TimeRangePatchChainTests
{
    internal class DummyClass
    {
        [JsonPropertyName("myProperty")]
        public string MyProperty { get; set; }
    }

    [Fact]
    public void Test_Contains()
    {
        var trpA = new TimeRangePatch(patch: null, from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero), to: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var trpB = new TimeRangePatch(patch: null, from: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var trpCollection = new TimeRangePatchChain<DummyClass>(new[] { trpA, trpB });
        trpCollection.Contains(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        trpCollection.Contains(new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        trpCollection.Contains(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void Test_Contains_With_Grace()
    {
        var trpA = new TimeRangePatch(patch: null, from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero), to: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var trpB = new TimeRangePatch(patch: null, from: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var trpCollection = new TimeRangePatchChain<DummyClass>(new[] { trpA, trpB });

        for (var i = -1000; i <= 1000; i += 100)
        {
            trpCollection.Contains(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(i)).Should().BeTrue();
            if (i != 0)
            {
                trpCollection.Contains(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(i), graceTicks: 0).Should().BeFalse();
            }
        }
        trpCollection.Contains(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(1001)).Should().BeFalse();
    }

    /// <summary>
    /// We mainly test the roundtrips, meaning: first apply patches, then ensure that applying the patch works as intended.
    /// So we don't care too much about our internals but mostly about self-consistency and integrity.
    /// </summary>
    [Theory]
    [InlineData(-1, "foo")]
    [InlineData(0, "bar")]
    [InlineData(1, "bar")]
    public void Test_Single_Patch(int daysToKeyDate, string expectedProperty)
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>();
        var myEntity = new DummyClass
        {
            MyProperty = "foo" // start with foo
        };
        var myChangedEntity = new DummyClass
        {
            MyProperty = "bar" // then switch to bar at key date
        };
        var keyDate = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, myChangedEntity, keyDate);
        var actual = trpCollection.PatchToDate(myEntity, keyDate + TimeSpan.FromDays(daysToKeyDate));
        actual.MyProperty.Should().Be(expectedProperty);
    }

    /// <summary>
    /// Apply to patches, in ascending order
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Sequentially()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>();
        var myEntity = new DummyClass
        {
            MyProperty = "foo" // start with foo
        };
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "bar" // switch to bar at keydate A
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateA);
        }
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "baz" // switch to baz at keydate B
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateB);
        }
        var actualA = trpCollection.PatchToDate(myEntity, keyDateA);
        actualA.MyProperty.Should().Be("bar");
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("baz");
    }

    /// <summary>
    /// Apply two patches but the second one is before the last one
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Unordered()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>();
        var myEntity = new DummyClass
        {
            MyProperty = "foo" // start with foo
        };
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "baz" // switch to bar at keydate B
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateB);
        }
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "bar" // switch to bar at keydate A (but this time apply the A patch _after_ the B patch
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateA, futurePatchBehaviour: FuturePatchBehaviour.KeepTheFuture);
        }
        var actualA = trpCollection.PatchToDate(myEntity, keyDateA);
        actualA.MyProperty.Should().Be("bar");
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("baz");
    }
}
