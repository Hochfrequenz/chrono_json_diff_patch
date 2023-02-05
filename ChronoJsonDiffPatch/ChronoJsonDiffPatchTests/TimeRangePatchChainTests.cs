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

        trpCollection.HasStart.Should().BeFalse();
        trpCollection.HasEnd.Should().BeFalse();
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

        trpCollection.HasStart.Should().BeFalse();
        trpCollection.HasEnd.Should().BeFalse();
        trpCollection.Where(p => p.End != DateTime.MaxValue).Should()
            .AllSatisfy(p => p.End.Second.Should().Be(0)) // no 23:59:59.9999, please
            .And.AllSatisfy(p => p.End.Minute.Should().Be(0))
            .And.AllSatisfy(p => p.Start.Kind.Should().Be(DateTimeKind.Utc))
            .And.AllSatisfy(p => p.End.Kind.Should().Be(DateTimeKind.Utc));
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

        trpCollection.HasStart.Should().BeFalse();
        trpCollection.HasEnd.Should().BeFalse();

        trpCollection.Where(p => p.End != DateTime.MaxValue).Should()
            .AllSatisfy(p => p.End.Second.Should().Be(0)) // no 23:59:59.9999, please
            .And.AllSatisfy(p => p.End.Minute.Should().Be(0))
            .And.AllSatisfy(p => p.Start.Kind.Should().Be(DateTimeKind.Utc))
            .And.AllSatisfy(p => p.End.Kind.Should().Be(DateTimeKind.Utc));
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

        trpCollection.HasStart.Should().BeFalse();
        trpCollection.HasEnd.Should().BeFalse();
        trpCollection.Where(p => p.End != DateTime.MaxValue).Should()
            .AllSatisfy(p => p.End.Second.Should().Be(0)) // no 23:59:59.9999, please
            .And.AllSatisfy(p => p.End.Minute.Should().Be(0))
            .And.AllSatisfy(p => p.Start.Kind.Should().Be(DateTimeKind.Utc))
            .And.AllSatisfy(p => p.End.Kind.Should().Be(DateTimeKind.Utc));
    }

    /// <summary>
    /// Apply three patches but discard the future
    /// </summary>
    [Fact]
    public void Test_Three_Patches_And_Overwrite_The_Future()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>();
        var myEntity = new DummyClass
        {
            MyProperty = "A" // start with foo
        };
        var keyDateC = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "C" // switch to "C" at keydate C
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateC, FuturePatchBehaviour.OverwriteTheFuture);
        }
        var actualC = trpCollection.PatchToDate(myEntity, keyDateC);
        actualC.MyProperty.Should().Be("C");
        var keyDateB = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "B" // switch to B at keydate B
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateB, futurePatchBehaviour: FuturePatchBehaviour.OverwriteTheFuture);
        }
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("B");

        actualC = trpCollection.PatchToDate(myEntity, keyDateC); // again
        actualC.MyProperty.Should().Be("B"); // not C because overwrite the future

        trpCollection.HasStart.Should().BeFalse();
        trpCollection.HasEnd.Should().BeFalse();
        trpCollection.Where(p => p.End != DateTime.MaxValue).Should()
            .AllSatisfy(p => p.End.Second.Should().Be(0)) // no 23:59:59.9999, please
            .And.AllSatisfy(p => p.End.Minute.Should().Be(0))
            .And.AllSatisfy(p => p.Start.Kind.Should().Be(DateTimeKind.Utc))
            .And.AllSatisfy(p => p.End.Kind.Should().Be(DateTimeKind.Utc));
    }

    [Fact]
    public void Test_Patching_Many_Dates()
    {
        var chain = new TimeRangePatchChain<DummyClass>();
        var initialEntity = new DummyClass
        {
            MyProperty = "initial"
        };
        var datesAndValues = new Dictionary<DateTimeOffset, string>
        {
            { new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero), "2022" },
            { new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero), "1990" },
            { new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), "2027" },
            { new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero), "2029" },
            { new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), "2025" },
        };
        foreach (var (patchDatetime, value) in datesAndValues)
        {
            var patchedEntity = new DummyClass
            {
                MyProperty = value
            };
            chain.Add(initialEntity, patchedEntity, patchDatetime, FuturePatchBehaviour.KeepTheFuture);
        }

        chain.Where(p => p.End != DateTime.MaxValue).Should()
            .AllSatisfy(p => p.Start.Month.Should().Be(1))
            .And.AllSatisfy(p => p.Start.Day.Should().Be(1))
            .And.AllSatisfy(p => p.End.Second.Should().Be(0)) // no 23:59:59.9999, please
            .And.AllSatisfy(p => p.End.Minute.Should().Be(0))
            .And.AllSatisfy(p => p.Start.Kind.Should().Be(DateTimeKind.Utc))
            .And.AllSatisfy(p => p.End.Kind.Should().Be(DateTimeKind.Utc));
        foreach (var (keyDate, beschreibung) in datesAndValues.Skip(1))
        {
            keyDate.Hour.Should().Be(0);
            var actualAtKeyDate = chain.PatchToDate(initialEntity, keyDate);
            actualAtKeyDate.MyProperty.Should().Be(beschreibung);
        }
    }
}
