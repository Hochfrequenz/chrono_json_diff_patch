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

    /// <summary>
    /// checks that the given <paramref name="chain"/> passes basic sanity checks
    /// </summary>
    /// <param name="initialState"></param>
    /// <param name="chain"></param>
    /// <param name="checkReverseChain"></param>
    private static void AssertBasicSanity(DummyClass initialState, TimeRangePatchChain<DummyClass> chain, bool checkReverseChain = true)
    {
        chain.HasStart.Should().BeFalse();
        chain.HasEnd.Should().BeFalse();

        chain.End.Should().Be(DateTimeOffset.MaxValue.UtcDateTime);
        chain.End.Year.Should().Be(DateTimeOffset.MaxValue.Year);
        chain.Last.End.Should().Be(DateTimeOffset.MaxValue.UtcDateTime);
        chain.Last.End.Year.Should().Be(DateTimeOffset.MaxValue.Year);

        chain.Where(p => p.End != DateTime.MaxValue).Should()
            .AllSatisfy(p => p.End.Second.Should().Be(0), because: "in our tests we only use second 0")
            .And.AllSatisfy(p => p.End.Minute.Should().Be(0), because: "in our tests we only use minute 0")
            .And.AllSatisfy(p => p.Start.Day.Should().Be(1), because: "in our tests we only use day 1")
            .And.AllSatisfy(p => p.End.Day.Should().Be(1), because: "in out tests we only use day 1")
            .And.AllSatisfy(p => p.Start.Kind.Should().Be(DateTimeKind.Utc))
            .And.AllSatisfy(p => p.End.Kind.Should().Be(DateTimeKind.Utc))
            .And.Subject.Cast<TimeRangePatch>().Should()
            .AllSatisfy(p => p.PatchingDirection.Should().Be(chain.PatchingDirection));

        chain.Where(p => p.End != DateTime.MaxValue).Should()
            .AllSatisfy(p => chain.Any(q => q.Start == p.End).Should().BeTrue(), because: "The ends of all entries p shall be the start of another entry q");
        chain.Where(p => p.Start != DateTime.MinValue).Should()
            .AllSatisfy(p => chain.Any(q => q.End == p.Start).Should().BeTrue(), because: "The starts of all entries p shall be the end of another entry q");
        if (!checkReverseChain)
        {
            return;
        }
        var (reversedInitialState, reversedChain) = chain.Reverse(initialEntity: initialState);
        reversedChain.PatchingDirection.Should().NotBe(chain.PatchingDirection);
        AssertBasicSanity(reversedInitialState, reversedChain, checkReverseChain = false);
        foreach (var patchDate in chain.Select(p => p.Start))
        {
            var stateInForwardChain = chain.PatchToDate(initialState, patchDate);
            var stateInBackwardChain = reversedChain.PatchToDate(reversedInitialState, patchDate);
            stateInBackwardChain.Should().BeEquivalentTo(stateInForwardChain, because: $"The states at {patchDate:O} should match");
            if (patchDate != DateTimeOffset.MinValue.UtcDateTime)
            {
                var slightlyBeforePatchDate = patchDate - TimeSpan.FromTicks(1);
                stateInForwardChain = chain.PatchToDate(initialState, slightlyBeforePatchDate);
                stateInBackwardChain = reversedChain.PatchToDate(reversedInitialState, slightlyBeforePatchDate);
                stateInBackwardChain.Should().BeEquivalentTo(stateInForwardChain, because: $"The states at {slightlyBeforePatchDate:O} should match");
            }

            var slightlyAfterPatchDate = patchDate + TimeSpan.FromTicks(1);
            stateInForwardChain = chain.PatchToDate(initialState, slightlyAfterPatchDate);
            stateInBackwardChain = reversedChain.PatchToDate(reversedInitialState, slightlyAfterPatchDate);
            stateInBackwardChain.Should().BeEquivalentTo(stateInForwardChain, because: $"The states at {slightlyAfterPatchDate:O} should match");
        }
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

        AssertBasicSanity(myEntity, trpCollection);
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
        var actualBeforePatch = trpCollection.PatchToDate(myEntity, new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero));
        actualBeforePatch.MyProperty.Should().Be("foo");

        var actualA = trpCollection.PatchToDate(myEntity, keyDateA);
        actualA.MyProperty.Should().Be("bar");
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("baz");

        AssertBasicSanity(myEntity, trpCollection);

        var (entityAtEndOfTime, reversedChain) = trpCollection.Reverse(myEntity);
        entityAtEndOfTime.Should().BeEquivalentTo(actualB);
        reversedChain.PatchingDirection.Should().Be(PatchingDirection.AntiparallelWithTime);

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
        var actualBeforePatch = trpCollection.PatchToDate(myEntity, new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero));
        actualBeforePatch.MyProperty.Should().Be("foo");

        var actualA = trpCollection.PatchToDate(myEntity, keyDateA);
        actualA.MyProperty.Should().Be("bar");
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("baz");

        AssertBasicSanity(myEntity, trpCollection);
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
        AssertBasicSanity(myEntity, trpCollection);

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
        AssertBasicSanity(myEntity, trpCollection);

        var actualC = trpCollection.PatchToDate(myEntity, keyDateC);
        actualC.MyProperty.Should().Be("C");
        AssertBasicSanity(myEntity, trpCollection);

        var actualD = trpCollection.PatchToDate(myEntity, keyDateD);
        actualD.MyProperty.Should().Be("D");
        AssertBasicSanity(myEntity, trpCollection);
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

        AssertBasicSanity(myEntity, trpCollection);
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
            AssertBasicSanity(initialEntity, chain);
        }

        var actualBeforePatch = chain.PatchToDate(initialEntity, new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero));
        actualBeforePatch.MyProperty.Should().Be(initialEntity.MyProperty);

        foreach (var (keyDate, beschreibung) in datesAndValues.Skip(1))
        {
            keyDate.Hour.Should().Be(0);
            var actualAtKeyDate = chain.PatchToDate(initialEntity, keyDate);
            actualAtKeyDate.MyProperty.Should().Be(beschreibung);
        }

        chain.End.Should().Be(DateTimeOffset.MaxValue.UtcDateTime);
        chain.Last.End.Should().Be(DateTimeOffset.MaxValue.UtcDateTime);
    }

    [Fact]
    public void Test_Patching_Add_Constructor()
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
            AssertBasicSanity(initialEntity, chain);
        }
        // now instantiate another chain by using the chains of the existing one:
        var anotherChain = new TimeRangePatchChain<DummyClass>(chain.GetAll()); // must not throw an exception
        anotherChain.Should().BeEquivalentTo(chain);
        AssertBasicSanity(initialEntity, anotherChain);
    }

    [Fact]
    public void Test_Patching_In_The_Past_Raises_Exception_If_no_Behaviour_Is_Specified()
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
        Action addInThePastWithoutSpecifyingBehaviour;
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "bar" // switch to bar at keydate A (but this time apply the A patch _after_ the B patch
            };
            addInThePastWithoutSpecifyingBehaviour = () => trpCollection.Add(myEntity, myAnotherEntity, keyDateA, futurePatchBehaviour: null);
        }
        addInThePastWithoutSpecifyingBehaviour.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Test_Patching_Backwards()
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
        AssertBasicSanity(myEntity, trpCollection);
        Action addInThePastWithoutSpecifyingBehaviour;
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "bar" // switch to bar at keydate A (but this time apply the A patch _after_ the B patch
            };
            addInThePastWithoutSpecifyingBehaviour = () => trpCollection.Add(myEntity, myAnotherEntity, keyDateA, futurePatchBehaviour: null);
        }
        addInThePastWithoutSpecifyingBehaviour.Should().Throw<ArgumentNullException>();
    }
}
