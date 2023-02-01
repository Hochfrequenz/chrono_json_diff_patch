using System.Text.Json.Serialization;
using ChronoJsonDiffPatch;
using FluentAssertions;
using Itenso.TimePeriod;

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
    /// Apply two patches but the second one (B) is before the last one (A)
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

    /// <summary>
    /// Apply three patches but the order is ADCB
    /// </summary>
    [Fact]
    public void Test_Four_Patches_Unordered()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>();
        var myEntity = new DummyClass
        {
            MyProperty = "A" // start with foo
        };
        var keyDateD = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "D" // switch to "D" at keydate D
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateD);
        }
        var keyDateC = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "C" // switch to C at keydate C
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateC, futurePatchBehaviour: FuturePatchBehaviour.KeepTheFuture);
        }
        var keyDateB = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "B" // switch to B at keydate B
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateB, futurePatchBehaviour: FuturePatchBehaviour.KeepTheFuture);
        }
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("B");

        var actualC = trpCollection.PatchToDate(myEntity, keyDateC);
        actualC.MyProperty.Should().Be("C");

        var actualD = trpCollection.PatchToDate(myEntity, keyDateD);
        actualD.MyProperty.Should().Be("D");
    }

    [Fact]
    public void Test_Insert_Into_A_TimePeriodChain_Without_An_End()
    {
        var itemAStart = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        itemAStart = DateTime.MinValue;
        var chain = new TimePeriodChain();
        var itemA = new TimeRange
        {
            Start = itemAStart,
            End = new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
        };
        var itemC = new TimeRange
        {
            Start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            End = DateTime.MaxValue
        };
        chain.Add(itemA);
        chain.Add(itemC);
        // now the chain looks like this:
        // Min     2023         2024         2025             Max
        //  |...----|------------|------------|-------------...|--> time
        //          [--itemA-----------------)[---itemC-----...)


        var itemB = new TimeRange
        {
            Start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            End = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        // now i want to insert itemB into the chain, such that
        // Min     2023         2024         2025             Max
        //  |...----|------------|------------|-------------...|--> time
        //          [--itemA----)[---itemB---)[----itemC----...)
        chain.Add(itemB); // works
    }
}
