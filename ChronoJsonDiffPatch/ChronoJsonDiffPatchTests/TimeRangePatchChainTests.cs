using System.Text.Json.Serialization;
using AwesomeAssertions;
using ChronoJsonDiffPatch;

namespace ChronoJsonDiffPatchTests;

public class TimeRangePatchChainTests
{
    internal class DummyClass
    {
        [JsonPropertyName("myProperty")]
        public string MyProperty { get; set; }
    }

    internal class DummyClassWithTwoProperties
    {
        [JsonPropertyName("myPropertyA")]
        public string MyPropertyA { get; set; }

        [JsonPropertyName("myPropertyB")]
        public string MyPropertyB { get; set; }
    }

    /// <summary>
    /// checks that the given <paramref name="chain"/> passes basic sanity checks
    /// </summary>
    /// <param name="initialState"></param>
    /// <param name="chain"></param>
    /// <param name="numberOfReverseChecks">how often should the chain be reversed and checked again for consistency? Any number &gt; 2 is not meaningful</param>
    private static void AssertBasicSanity<TBaseClass>(
        TBaseClass initialState,
        TimeRangePatchChain<TBaseClass> chain,
        int numberOfReverseChecks = 2
    )
    {
        if (numberOfReverseChecks is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(numberOfReverseChecks),
                $"should be between 0 <= {nameof(numberOfReverseChecks)} <= 4"
            );
        }
        chain.HasStart.Should().BeFalse();
        chain.HasEnd.Should().BeFalse();

        chain.End.Should().Be(DateTimeOffset.MaxValue.UtcDateTime);
        chain.End.Year.Should().Be(DateTimeOffset.MaxValue.Year);
        chain.Last.End.Should().Be(DateTimeOffset.MaxValue.UtcDateTime);
        chain.Last.End.Year.Should().Be(DateTimeOffset.MaxValue.Year);

        // please don't ever add 23:59:59 or alike.
        chain
            .Where(p => p.End != DateTime.MaxValue)
            .Should()
            .AllSatisfy(
                p => p.End.Second.Should().Be(0),
                because: "in our tests we only use second 0"
            )
            .And.AllSatisfy(
                p => p.End.Minute.Should().Be(0),
                because: "in our tests we only use minute 0"
            )
            .And.AllSatisfy(
                p => p.Start.Day.Should().Be(1),
                because: "in our tests we only use day 1"
            )
            .And.AllSatisfy(
                p => p.End.Day.Should().Be(1),
                because: "in out tests we only use day 1"
            )
            .And.AllSatisfy(p => p.Start.Kind.Should().Be(DateTimeKind.Utc))
            .And.AllSatisfy(p => p.End.Kind.Should().Be(DateTimeKind.Utc))
            .And.Subject.Cast<TimeRangePatch>()
            .Should()
            .AllSatisfy(p => p.PatchingDirection.Should().Be(chain.PatchingDirection));

        chain
            .Where(p => p.End != DateTime.MaxValue)
            .Should()
            .AllSatisfy(
                p => chain.Any(q => q.Start == p.End).Should().BeTrue(),
                because: "The ends of all entries p shall be the start of another entry q"
            );
        chain
            .Where(p => p.Start != DateTime.MinValue)
            .Should()
            .AllSatisfy(
                p => chain.Any(q => q.End == p.Start).Should().BeTrue(),
                because: "The starts of all entries p shall be the end of another entry q"
            );
        if (numberOfReverseChecks == 0)
        {
            return;
        }
        var (reversedInitialState, reversedChain) = chain.Reverse(initialEntity: initialState);
        reversedChain.PatchingDirection.Should().NotBe(chain.PatchingDirection);
        AssertBasicSanity(
            reversedInitialState,
            reversedChain,
            numberOfReverseChecks = numberOfReverseChecks - 1
        );
        foreach (var patchDate in chain.Select(p => p.Start))
        {
            var stateInForwardChain = chain.PatchToDate(initialState, patchDate);
            var stateInBackwardChain = reversedChain.PatchToDate(reversedInitialState, patchDate);
            stateInBackwardChain
                .Should()
                .BeEquivalentTo(
                    stateInForwardChain,
                    because: $"The states at {patchDate:O} should match"
                );
            if (patchDate != DateTimeOffset.MinValue.UtcDateTime)
            {
                var slightlyBeforePatchDate = patchDate - TimeSpan.FromTicks(1);
                stateInForwardChain = chain.PatchToDate(initialState, slightlyBeforePatchDate);
                stateInBackwardChain = reversedChain.PatchToDate(
                    reversedInitialState,
                    slightlyBeforePatchDate
                );
                stateInBackwardChain
                    .Should()
                    .BeEquivalentTo(
                        stateInForwardChain,
                        because: $"The states at {slightlyBeforePatchDate:O} should match"
                    );
            }

            var slightlyAfterPatchDate = patchDate + TimeSpan.FromTicks(1);
            stateInForwardChain = chain.PatchToDate(initialState, slightlyAfterPatchDate);
            stateInBackwardChain = reversedChain.PatchToDate(
                reversedInitialState,
                slightlyAfterPatchDate
            );
            stateInBackwardChain
                .Should()
                .BeEquivalentTo(
                    stateInForwardChain,
                    because: $"The states at {slightlyAfterPatchDate:O} should match"
                );
        }

        var reversedChainDirection = reversedChain.PatchingDirection;
        var copiedReversedChain = new TimeRangePatchChain<DummyClass>(
            reversedChain.GetAll(),
            reversedChainDirection
        );
        copiedReversedChain.Should().BeEquivalentTo(reversedChain);
    }

    [Fact]
    public void Test_Contains()
    {
        var trpA = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var trpB = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var trpCollection = new TimeRangePatchChain<DummyClass>(new[] { trpA, trpB });
        trpCollection
            .Contains(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .Should()
            .BeTrue();
        trpCollection
            .Contains(new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .Should()
            .BeTrue();
        trpCollection
            .Contains(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void Test_Contains_With_Grace()
    {
        var trpA = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var trpB = new TimeRangePatch(
            patch: null,
            from: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        var trpCollection = new TimeRangePatchChain<DummyClass>(new[] { trpA, trpB });

        for (var i = -1000; i <= 1000; i += 100)
        {
            trpCollection
                .Contains(
                    new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(i)
                )
                .Should()
                .BeTrue();
            if (i != 0)
            {
                trpCollection
                    .Contains(
                        new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)
                            + TimeSpan.FromTicks(i),
                        graceTicks: 0
                    )
                    .Should()
                    .BeFalse();
            }
        }
        trpCollection
            .Contains(
                new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(1001)
            )
            .Should()
            .BeFalse();
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
            MyProperty = "foo", // start with foo
        };
        var myChangedEntity = new DummyClass
        {
            MyProperty = "bar", // then switch to bar at key date
        };
        var keyDate = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, myChangedEntity, keyDate);
        var actual = trpCollection.PatchToDate(
            myEntity,
            keyDate + TimeSpan.FromDays(daysToKeyDate)
        );
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
            MyProperty = "foo", // start with foo
        };
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "bar", // switch to bar at keydate A
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateA);
        }
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "baz", // switch to baz at keydate B
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateB);
        }
        var actualBeforePatch = trpCollection.PatchToDate(
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        actualBeforePatch.MyProperty.Should().Be("foo");

        var actualA = trpCollection.PatchToDate(myEntity, keyDateA);
        actualA.MyProperty.Should().Be("bar");
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("baz");

        AssertBasicSanity(myEntity, trpCollection);

        var (entityAtEndOfTime, reversedChain) = trpCollection.Reverse(myEntity);
        entityAtEndOfTime.Should().BeEquivalentTo(actualB);
        reversedChain.PatchingDirection.Should().Be(PatchingDirection.AntiParallelWithTime);
    }

    /// <summary>
    /// Apply to patches, in ascending order, replace the last one with a second patch
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Sequentially_with_the_last_one_being_at_the_same_date()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>();
        var myEntity = new DummyClass
        {
            MyProperty = "foo", // start with foo
        };
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "bar", // switch to bar at keydate A
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateA);
        }
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "old-baz1", // switch to baz at keydate B
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateB);
        }
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "new-baz", // switch to new-baz, also at keydate B
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateB);
        }
        var actualBeforePatch = trpCollection.PatchToDate(
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        actualBeforePatch.MyProperty.Should().Be("foo");

        var actualA = trpCollection.PatchToDate(myEntity, keyDateA);
        actualA.MyProperty.Should().Be("bar");
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("new-baz");

        AssertBasicSanity(myEntity, trpCollection);
    }

    /// <summary>
    /// Apply to patches, in ascending order, replace the second one with a second patch
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Sequentially_with_middle_last_one_being_at_the_same_date_keep_the_future()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>();
        var myEntity = new DummyClass
        {
            MyProperty = "foo", // start with foo
        };
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "bar1", // switch to bar1 at keydate A
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateA);
        }
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "baz", // switch to baz at keydate B
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateB);
        }
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "bar2", // now switch to bar2 at keydate A and overwrite the existing bar1 without loosing the following baz
            };
            trpCollection.Add(
                myEntity,
                myChangedEntity,
                keyDateA,
                FuturePatchBehaviour.KeepTheFuture
            );
        }

        var actualBeforePatch = trpCollection.PatchToDate(
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        actualBeforePatch.MyProperty.Should().Be("foo");
        var actualA = trpCollection.PatchToDate(myEntity, keyDateA);
        actualA.MyProperty.Should().Be("bar2");
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("baz");

        AssertBasicSanity(myEntity, trpCollection);
    }

    /// <summary>
    /// Apply to patches, in ascending order, replace the second one with a second patch
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Sequentially_with_middle_last_one_being_at_the_same_date_overwrite_the_future()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>();
        var myEntity = new DummyClass
        {
            MyProperty = "foo", // start with foo
        };
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "bar1", // switch to bar1 at keydate A
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateA);
        }
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "baz", // switch to baz at keydate B
            };
            trpCollection.Add(myEntity, myAnotherEntity, keyDateB);
        }
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "bar2", // now switch to bar2 at keydate A and overwrite the existing bar1; discard the future
            };
            trpCollection.Add(
                myEntity,
                myChangedEntity,
                keyDateA,
                FuturePatchBehaviour.OverwriteTheFuture
            );
        }

        var actualBeforePatch = trpCollection.PatchToDate(
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        actualBeforePatch.MyProperty.Should().Be("foo");
        var actualA = trpCollection.PatchToDate(myEntity, keyDateA);
        actualA.MyProperty.Should().Be("bar2");
        var actualB = trpCollection.PatchToDate(myEntity, keyDateB);
        actualB.MyProperty.Should().Be("bar2");

        AssertBasicSanity(myEntity, trpCollection);
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
            MyProperty = "foo", // start with foo
        };
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "baz", // switch to bar at keydate B
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateB);
        }
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "bar", // switch to bar at keydate A (but this time apply the A patch _after_ the B patch
            };
            trpCollection.Add(
                myEntity,
                myAnotherEntity,
                keyDateA,
                futurePatchBehaviour: FuturePatchBehaviour.KeepTheFuture
            );
        }
        var actualBeforePatch = trpCollection.PatchToDate(
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
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
            MyProperty = "A", // start with foo
        };
        var keyDateD = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "D", // switch to "D" at keydate D
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateD);
        }
        var keyDateC = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "C", // switch to C at keydate C
            };
            trpCollection.Add(
                myEntity,
                myAnotherEntity,
                keyDateC,
                futurePatchBehaviour: FuturePatchBehaviour.KeepTheFuture
            );
        }
        AssertBasicSanity(myEntity, trpCollection);

        var keyDateB = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "B", // switch to B at keydate B
            };
            trpCollection.Add(
                myEntity,
                myAnotherEntity,
                keyDateB,
                futurePatchBehaviour: FuturePatchBehaviour.KeepTheFuture
            );
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
            MyProperty = "A", // start with foo
        };
        var keyDateC = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "C", // switch to "C" at keydate C
            };
            trpCollection.Add(
                myEntity,
                myChangedEntity,
                keyDateC,
                FuturePatchBehaviour.OverwriteTheFuture
            );
        }
        var actualC = trpCollection.PatchToDate(myEntity, keyDateC);
        actualC.MyProperty.Should().Be("C");
        var keyDateB = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "B", // switch to B at keydate B
            };
            trpCollection.Add(
                myEntity,
                myAnotherEntity,
                keyDateB,
                futurePatchBehaviour: FuturePatchBehaviour.OverwriteTheFuture
            );
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
        var initialEntity = new DummyClass { MyProperty = "initial" };
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
            var patchedEntity = new DummyClass { MyProperty = value };
            chain.Add(
                initialEntity,
                patchedEntity,
                patchDatetime,
                FuturePatchBehaviour.KeepTheFuture
            );
            AssertBasicSanity(initialEntity, chain);
        }

        var actualBeforePatch = chain.PatchToDate(
            initialEntity,
            new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
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
        var initialEntity = new DummyClass { MyProperty = "initial" };
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
            var patchedEntity = new DummyClass { MyProperty = value };
            chain.Add(
                initialEntity,
                patchedEntity,
                patchDatetime,
                FuturePatchBehaviour.KeepTheFuture
            );
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
            MyProperty = "foo", // start with foo
        };
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "baz", // switch to bar at keydate B
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDateB);
        }
        Action addInThePastWithoutSpecifyingBehaviour;
        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myAnotherEntity = new DummyClass
            {
                MyProperty = "bar", // switch to bar at keydate A (but this time apply the A patch _after_ the B patch
            };
            addInThePastWithoutSpecifyingBehaviour = () =>
                trpCollection.Add(myEntity, myAnotherEntity, keyDateA, futurePatchBehaviour: null);
        }
        addInThePastWithoutSpecifyingBehaviour.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Test_Patching_On_Same_Date()
    {
        var trpCollection = new TimeRangePatchChain<DummyClassWithTwoProperties>();
        var myEntity = new DummyClassWithTwoProperties { MyPropertyA = "A0", MyPropertyB = "B0" };
        var keyDate1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "A1",
                MyPropertyB = "B1",
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDate1);
        }
        {
            var myChangedEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "A1", // A stays at A1
                MyPropertyB = "B2", // but B changes to B2
            };
            trpCollection.Add(
                myEntity,
                myChangedEntity,
                keyDate1,
                futurePatchBehaviour: FuturePatchBehaviour.KeepTheFuture
            );
        }
        AssertBasicSanity(myEntity, trpCollection);
        var entityAtKeyDate0 = trpCollection.PatchToDate(myEntity, DateTimeOffset.MinValue);
        entityAtKeyDate0.MyPropertyA.Should().Be("A0");
        entityAtKeyDate0.MyPropertyB.Should().Be("B0");

        var entityAtKeyDate1 = trpCollection.PatchToDate(myEntity, keyDate1);
        entityAtKeyDate1.MyPropertyA.Should().Be("A1");
        entityAtKeyDate1.MyPropertyB.Should().Be("B2");
    }

    [Fact]
    public void Test_Patching_On_Same_Date_In_The_Past()
    {
        var trpCollection = new TimeRangePatchChain<DummyClassWithTwoProperties>();
        var myEntity = new DummyClassWithTwoProperties { MyPropertyA = "A0", MyPropertyB = "B0" };
        var keyDate1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "A1",
                MyPropertyB = "B1",
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDate1);
        }
        var entityAtKeyDate1 = trpCollection.PatchToDate(myEntity, keyDate1);
        entityAtKeyDate1.MyPropertyA.Should().Be("A1");
        entityAtKeyDate1.MyPropertyB.Should().Be("B1");
        var keydate0b = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "A0.5",
                MyPropertyB = "B0.5",
            };
            trpCollection.Add(
                myEntity,
                myChangedEntity,
                keydate0b,
                FuturePatchBehaviour.KeepTheFuture
            );
        }
        var keyDate3 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "A3",
                MyPropertyB = "B3",
            };
            trpCollection.Add(
                myEntity,
                myChangedEntity,
                keyDate3,
                futurePatchBehaviour: FuturePatchBehaviour.KeepTheFuture
            );
        }
        {
            var myChangedEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "A1", // at keydate1 a stays A1
                MyPropertyB = "B2", // but we switch property B to B2
            };
            // note that in this test there is already a later patch after keydate1
            trpCollection.Add(
                myEntity,
                myChangedEntity,
                keyDate1,
                FuturePatchBehaviour.KeepTheFuture
            );
        }
        AssertBasicSanity(myEntity, trpCollection);
        var entityAtKeyDate0 = trpCollection.PatchToDate(myEntity, DateTimeOffset.MinValue);
        entityAtKeyDate0.MyPropertyA.Should().Be("A0");
        entityAtKeyDate0.MyPropertyB.Should().Be("B0");

        var entityAtKeyDate05 = trpCollection.PatchToDate(myEntity, keydate0b);
        entityAtKeyDate05.MyPropertyA.Should().Be("A0.5");
        entityAtKeyDate05.MyPropertyB.Should().Be("B0.5");

        entityAtKeyDate1 = trpCollection.PatchToDate(myEntity, keyDate1);
        entityAtKeyDate1.MyPropertyA.Should().Be("A1");
        entityAtKeyDate1.MyPropertyB.Should().Be("B2");

        var entityAtKeyDate3 = trpCollection.PatchToDate(myEntity, keyDate3);
        entityAtKeyDate3.MyPropertyA.Should().Be("A3");
        entityAtKeyDate3.MyPropertyB.Should().Be("B3");
    }

    [Fact]
    public void Test_Patching_Different_Properties_Unordered()
    {
        var trpCollection = new TimeRangePatchChain<DummyClassWithTwoProperties>();
        var myEntity = new DummyClassWithTwoProperties { MyPropertyA = "A0", MyPropertyB = "B0" };
        var keyDate2 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var myChangedEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "emptyA",
                MyPropertyB = "B2",
            };
            trpCollection.Add(myEntity, myChangedEntity, keyDate2);
        }
        var entityAtKeyDate2 = trpCollection.PatchToDate(myEntity, keyDate2);
        entityAtKeyDate2.MyPropertyA.Should().Be("emptyA");
        entityAtKeyDate2.MyPropertyB.Should().Be("B2");

        var keyDate1 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        keyDate1.Should().BeBefore(keyDate2);
        {
            var myChangedEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "A1",
                MyPropertyB = "B2",
            };
            trpCollection.Add(
                myEntity,
                myChangedEntity,
                keyDate1,
                FuturePatchBehaviour.KeepTheFuture
            );
        }
        AssertBasicSanity(myEntity, trpCollection);

        var entityAtKeyDate1 = trpCollection.PatchToDate(myEntity, keyDate1);
        entityAtKeyDate1.MyPropertyA.Should().Be("A1");
        entityAtKeyDate1.MyPropertyB.Should().Be("B2");

        entityAtKeyDate2 = trpCollection.PatchToDate(myEntity, keyDate2);
        entityAtKeyDate2.MyPropertyA.Should().Be("emptyA"); // this is a hard one: because of the "KeepTheFuture" the state of myEntity at key1 is reverted at keydate2
        // this might seem counter intuitive but it's technically correct unless we introduce something like property-specific patching behaviour, which would e.g. specify
        // that keep-the-future implies that the future should only be kept where future patches actually will have been modified (futur II) a property.
        entityAtKeyDate2.MyPropertyB.Should().Be("B2");
    }

    [Fact]
    public void Test_Patching_Backwards()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>(
            patchingDirection: PatchingDirection.AntiParallelWithTime
        );
        var myEntity = new DummyClass
        {
            MyProperty = "foo", // start with foo at +infinity
        };
        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Action addAction;
        {
            var myChangedEntity = new DummyClass
            {
                MyProperty = "bar", // before the keydate B the value was bar
            };
            addAction = () => trpCollection.Add(myEntity, myChangedEntity, keyDateB);
        }
        addAction.Should().Throw<NotImplementedException>();
        /*
        var stateAtKeydateB = trpCollection.PatchToDate(myEntity, keyDateB);
        stateAtKeydateB.MyProperty.Should().Be("foo");
        var stateAfterKeydateB = trpCollection.PatchToDate(myEntity, keyDateB + TimeSpan.FromTicks(1));
        stateAfterKeydateB.MyProperty.Should().Be("foo");
        var stateBeforeKeyDateB = trpCollection.PatchToDate(myEntity, keyDateB - TimeSpan.FromTicks(1));
        stateBeforeKeyDateB.MyProperty.Should().Be("bar");
        AssertBasicSanity(myEntity, trpCollection);
        */
    }

    [Fact]
    public void Test_Patching_Backwards_Throws_Meaningful_Error_For_Inconsistent_Data()
    {
        var trpCollection = new TimeRangePatchChain<DummyClass>(
            patchingDirection: PatchingDirection.ParallelWithTime
        );
        var myEntity = new DummyClass { MyProperty = "Foo" };
        {
            var keyDate1 = new DateTimeOffset(2034, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var myChangedEntity = new DummyClass { MyProperty = "Bar" };
            trpCollection.Add(myEntity, myChangedEntity, keyDate1);
        }
        {
            var keyDate2 = new DateTimeOffset(2035, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var myChangedEntity = new DummyClass { MyProperty = "Baz" };
            trpCollection.Add(myEntity, myChangedEntity, keyDate2);
        }
        var allPatches = trpCollection.GetAll().ToList();
        allPatches.Should().HaveCount(3);

        allPatches[1].End = DateTime.MaxValue; // let's a create chain, that is no longer self-consistent and has 2 elements with +infinity as end date
        allPatches.Where(p => p.End == DateTime.MaxValue).Should().HaveCount(2);
        Action creatingAChainFromInconsistentPatches = () =>
            new TimeRangePatchChain<DummyClass>(allPatches, PatchingDirection.ParallelWithTime);
        creatingAChainFromInconsistentPatches
            .Should()
            .Throw<ArgumentException>()
            .Where(ae => ae.Message.Contains("The given periods contain ambiguous starts"))
            .And.InnerException.Should()
            .BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Test_PatchToDate_With_TargetEntity_Preserves_Identity()
    {
        // Arrange: Create a chain with populateEntity action
        var trpCollection = new TimeRangePatchChain<DummyClass>(
            populateEntity: (json, target) =>
                Newtonsoft.Json.JsonConvert.PopulateObject(json, target)
        );

        var initialEntity = new DummyClass { MyProperty = "Initial" };
        var changedEntity = new DummyClass { MyProperty = "Changed" };
        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(initialEntity, changedEntity, keyDate);

        // Act: Create a target entity and populate it using PatchToDate
        var targetEntity = new DummyClass { MyProperty = "ORM-Tracked-Instance" };
        var originalReference = targetEntity; // Keep reference to verify identity

        trpCollection.PatchToDate(initialEntity, keyDate, targetEntity);

        // Assert: The target entity is the same instance (identity preserved)
        ReferenceEquals(targetEntity, originalReference).Should().BeTrue();
        targetEntity.MyProperty.Should().Be("Changed");
    }

    [Fact]
    public void Test_PatchToDate_With_TargetEntity_AntiParallelWithTime()
    {
        // Arrange: Create a forward chain, then reverse it
        var forwardChain = new TimeRangePatchChain<DummyClass>(
            populateEntity: (json, target) =>
                Newtonsoft.Json.JsonConvert.PopulateObject(json, target)
        );

        var initialEntity = new DummyClass { MyProperty = "Initial" };
        var changedEntity = new DummyClass { MyProperty = "Changed" };
        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        forwardChain.Add(initialEntity, changedEntity, keyDate);

        var (stateAtPlusInfinity, reversedChain) = forwardChain.Reverse(initialEntity);

        // Create a new chain with the reversed patches and populateEntity
        var antiParallelChain = new TimeRangePatchChain<DummyClass>(
            reversedChain.GetAll(),
            PatchingDirection.AntiParallelWithTime,
            populateEntity: (json, target) =>
                Newtonsoft.Json.JsonConvert.PopulateObject(json, target)
        );

        // Act: Populate target entity at a date before the change
        var targetEntity = new DummyClass { MyProperty = "ORM-Tracked-Instance" };
        var originalReference = targetEntity;
        var beforeKeyDate = new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero);

        antiParallelChain.PatchToDate(stateAtPlusInfinity, beforeKeyDate, targetEntity);

        // Assert: Identity preserved and value correct
        ReferenceEquals(targetEntity, originalReference).Should().BeTrue();
        targetEntity.MyProperty.Should().Be("Initial");
    }

    [Fact]
    public void Test_PatchToDate_With_TargetEntity_Throws_When_No_PopulateEntity_Provided()
    {
        // Arrange: Create a chain WITHOUT populateEntity action
        var trpCollection = new TimeRangePatchChain<DummyClass>();

        var initialEntity = new DummyClass { MyProperty = "Initial" };
        var changedEntity = new DummyClass { MyProperty = "Changed" };
        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(initialEntity, changedEntity, keyDate);

        var targetEntity = new DummyClass { MyProperty = "Target" };

        // Act & Assert
        Action patchWithTarget = () =>
            trpCollection.PatchToDate(initialEntity, keyDate, targetEntity);

        patchWithTarget.Should().Throw<InvalidOperationException>().WithMessage("*populateEntity*");
    }

    [Fact]
    public void Test_PatchToDate_With_TargetEntity_Multiple_Patches()
    {
        // Arrange: Create a chain with multiple patches
        var trpCollection = new TimeRangePatchChain<DummyClass>(
            populateEntity: (json, target) =>
                Newtonsoft.Json.JsonConvert.PopulateObject(json, target)
        );

        var initialEntity = new DummyClass { MyProperty = "V1" };
        trpCollection.Add(
            initialEntity,
            new DummyClass { MyProperty = "V2" },
            new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        trpCollection.Add(
            initialEntity,
            new DummyClass { MyProperty = "V3" },
            new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        trpCollection.Add(
            initialEntity,
            new DummyClass { MyProperty = "V4" },
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );

        // Act & Assert: Test at different dates
        var targetEntity = new DummyClass { MyProperty = "ORM-Tracked" };
        var originalReference = targetEntity;

        // At V2
        trpCollection.PatchToDate(
            initialEntity,
            new DateTimeOffset(2022, 6, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        ReferenceEquals(targetEntity, originalReference).Should().BeTrue();
        targetEntity.MyProperty.Should().Be("V2");

        // At V3
        trpCollection.PatchToDate(
            initialEntity,
            new DateTimeOffset(2023, 6, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        ReferenceEquals(targetEntity, originalReference).Should().BeTrue();
        targetEntity.MyProperty.Should().Be("V3");

        // At V4
        trpCollection.PatchToDate(
            initialEntity,
            new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        ReferenceEquals(targetEntity, originalReference).Should().BeTrue();
        targetEntity.MyProperty.Should().Be("V4");
    }

    [Fact]
    public void Test_PatchToDate_With_TargetEntity_Returns_Same_Result_As_Regular_PatchToDate()
    {
        // Arrange
        var trpCollection = new TimeRangePatchChain<DummyClassWithTwoProperties>(
            populateEntity: (json, target) =>
                Newtonsoft.Json.JsonConvert.PopulateObject(json, target)
        );

        var initialEntity = new DummyClassWithTwoProperties
        {
            MyPropertyA = "A1",
            MyPropertyB = "B1",
        };
        trpCollection.Add(
            initialEntity,
            new DummyClassWithTwoProperties { MyPropertyA = "A2", MyPropertyB = "B2" },
            new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
        trpCollection.Add(
            initialEntity,
            new DummyClassWithTwoProperties { MyPropertyA = "A3", MyPropertyB = "B3" },
            new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );

        var keyDate = new DateTimeOffset(2022, 6, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var resultFromRegularPatchToDate = trpCollection.PatchToDate(initialEntity, keyDate);

        var targetEntity = new DummyClassWithTwoProperties { MyPropertyA = "X", MyPropertyB = "Y" };
        trpCollection.PatchToDate(initialEntity, keyDate, targetEntity);

        // Assert: Both should have the same values
        targetEntity.MyPropertyA.Should().Be(resultFromRegularPatchToDate.MyPropertyA);
        targetEntity.MyPropertyB.Should().Be(resultFromRegularPatchToDate.MyPropertyB);
    }
}
