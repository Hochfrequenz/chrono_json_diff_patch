using System.Text.Json.Serialization;
using AwesomeAssertions;
using ChronoJsonDiffPatch;

namespace ChronoJsonDiffPatchTests;

public class ListPatchingTests
{
    internal record ListItem
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    internal record EntityWithList
    {
        [JsonPropertyName("myList")]
        public List<ListItem> MyList { get; set; }
    }

    [Fact]
    public void Test_List_Patching_Generally_Works_With_Add_And_Reverse()
    {
        var chain = new TimeRangePatchChain<EntityWithList>();
        var initialEntity = new EntityWithList
        {
            MyList = new List<ListItem>
            {
                new() { Value = "Foo" },
                new() { Value = "Bar" },
            },
        };
        {
            var updatedEntity1 = new EntityWithList
            {
                MyList = new List<ListItem>
                {
                    new() { Value = "fOO" },
                    new() { Value = "bAR" },
                },
            };
            var keyDate1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            chain.Add(initialEntity, updatedEntity1, keyDate1);

            chain.Count.Should().Be(2); // [-infinity, keyDate1); [keyDate1, +infinity)
            ReverseAndRevert(chain, initialEntity);
        }

        {
            var updatedEntity2 = new EntityWithList
            {
                MyList = new List<ListItem>
                {
                    new() { Value = "fOO" },
                    new() { Value = "bAR" },
                    new() { Value = "bAZ" },
                },
            };
            var keyDate2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            chain.Add(initialEntity, updatedEntity2, keyDate2);

            chain.Count.Should().Be(3); // [-infinity, keyDate1); [keyDate1, keyDate2); [keyDate2, +infinity)
            ReverseAndRevert(chain, initialEntity);
        }

        {
            var updatedEntity3 = new EntityWithList
            {
                MyList = new List<ListItem>
                {
                    new() { Value = "Not so foo anymore" },
                    new() { Value = "bAR" },
                    new() { Value = "bAZ" },
                },
            };
            var keyDate3 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            chain.Add(initialEntity, updatedEntity3, keyDate3);

            ReverseAndRevert(chain, initialEntity);
        }
    }

    /// <summary>
    /// In <see cref="Test_List_Patching_Generally_Works_With_Add_And_Reverse"/> we showed that adding and removing list entries is generally well-supported by this library.
    /// In this test, we show, than when users run into an <see cref="ArgumentOutOfRangeException"/>, this is probably due to initial entities not matching the expected state (corrupted).
    /// </summary>
    [Fact]
    public void Reproduce_ArgumentOutOfRangeException()
    {
        var chain = new TimeRangePatchChain<EntityWithList>();
        var initialEntity = new EntityWithList
        {
            MyList = new List<ListItem> { new() { Value = "My First Value" } },
        };
        var keyDate1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var updatedEntity1 = new EntityWithList
            {
                MyList = new List<ListItem>
                {
                    new() { Value = "My First Value" },
                    new() { Value = "My Second Value" },
                },
            };

            chain.Add(initialEntity, updatedEntity1, keyDate1);
            chain.Count.Should().Be(2); // [-infinity, keyDate1); [keyDate1, +infinity)
            ReverseAndRevert(chain, initialEntity);
        }
        (var antiparallelInitialEntity, var antiparallelChain) = chain.Reverse(initialEntity);
        antiparallelInitialEntity
            .Should()
            .Match<EntityWithList>(
                x => x.MyList.Count == 2,
                because: "Initially the list had 2 items"
            );
        var patchingACorrectInitialEntity = () =>
            antiparallelChain.PatchToDate(
                antiparallelInitialEntity,
                keyDate1 - TimeSpan.FromDays(10)
            );
        patchingACorrectInitialEntity.Should().NotThrow();

        var corruptedInitialEntity = antiparallelInitialEntity; // we modify the reference here, but that's fine. We improve the readability but don't re-use the antiparallelInitialEntity anywhere downstream.
        corruptedInitialEntity.MyList.RemoveAt(1);
        var applyingPatchesToACorruptedInitialEntity = () =>
            antiparallelChain.PatchToDate(corruptedInitialEntity, keyDate1 - TimeSpan.FromDays(10));
        applyingPatchesToACorruptedInitialEntity
            .Should()
            .ThrowExactly<PatchingException<EntityWithList>>()
            .Which.InnerException.Should()
            .NotBeNull()
            .And.Subject.Should()
            .BeOfType<ArgumentOutOfRangeException>();
        antiparallelChain.PatchesHaveBeenSkipped.Should().BeFalse();
    }

    /// <summary>
    /// Shows that the error from <see cref="Reproduce_ArgumentOutOfRangeException"/> can be surpressed using a <see cref="ISkipCondition{TEntity}"/>.
    /// </summary>
    [Fact]
    public void Test_ArgumentOutOfRangeException_Can_Be_Surpressed()
    {
        var chain = new TimeRangePatchChain<EntityWithList>(
            skipConditions: new List<ISkipCondition<EntityWithList>>
            {
                new SkipPatchesWithUnmatchedListItems<EntityWithList, ListItem>(x => x.MyList),
            }
        );
        var initialEntity = new EntityWithList
        {
            MyList = new List<ListItem> { new() { Value = "My First Value" } },
        };
        var keyDate1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        {
            var updatedEntity1 = new EntityWithList
            {
                MyList = new List<ListItem>
                {
                    new() { Value = "My First Value" },
                    new() { Value = "My Second Value" },
                },
            };

            chain.Add(initialEntity, updatedEntity1, keyDate1);
            chain.Count.Should().Be(2); // [-infinity, keyDate1); [keyDate1, +infinity)
            ReverseAndRevert(chain, initialEntity);
        }
        (var antiparallelInitialEntity, var antiparallelChain) = chain.Reverse(initialEntity);
        antiparallelInitialEntity
            .Should()
            .Match<EntityWithList>(
                x => x.MyList.Count == 2,
                because: "Initially the list had 2 items"
            );
        var patchingACorrectInitialEntity = () =>
            antiparallelChain.PatchToDate(
                antiparallelInitialEntity,
                keyDate1 - TimeSpan.FromDays(10)
            );
        patchingACorrectInitialEntity.Should().NotThrow();

        var corruptedInitialEntity = antiparallelInitialEntity; // we modify the reference here, but that's fine. We improve the readability but don't re-use the antiparallelInitialEntity anywhere downstream.
        corruptedInitialEntity.MyList.RemoveAt(1);
        var applyingPatchesToACorruptedInitialEntity = () =>
            antiparallelChain.PatchToDate(corruptedInitialEntity, keyDate1 - TimeSpan.FromDays(10));
        applyingPatchesToACorruptedInitialEntity
            .Should()
            .NotThrow()
            .And.Subject.Invoke()
            .Should()
            .BeEquivalentTo(corruptedInitialEntity);
        antiparallelChain.PatchesHaveBeenSkipped.Should().BeTrue();
    }

    /// <summary>
    /// Reproduces a scenario where the final deserialization after patching fails (e.g. because the
    /// accumulated JToken has a structurally invalid list), and verifies that WITHOUT skip conditions
    /// the exception propagates to the caller.
    /// </summary>
    /// <remarks>
    /// This simulates a production issue (DEV-107694 in TechnicalMasterData) where:
    /// - The initial entity had a null list (because of a missing EF Core Include)
    /// - Patches that added/modified list items were applied to the null JToken
    /// - The accumulated JToken had a structurally invalid list representation
    /// - The final System.Text.Json deserialization threw a JsonException
    ///
    /// The custom deserializer here simulates this by always throwing when deserializing.
    /// </remarks>
    [Fact]
    public void PatchToDate_Without_SkipConditions_Throws_When_Final_Deserialization_Fails()
    {
        // Build a valid chain first with the default deserializer
        var buildChain = new TimeRangePatchChain<EntityWithList>();
        var initialEntity = new EntityWithList
        {
            MyList = new List<ListItem> { new() { Value = "A" } },
        };
        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var updatedEntity = new EntityWithList
        {
            MyList = new List<ListItem>
            {
                new() { Value = "A" },
                new() { Value = "B" },
            },
        };
        buildChain.Add(initialEntity, updatedEntity, keyDate);

        // Extract raw patches and create a chain with a broken deserializer
        var patches = buildChain.GetAll().ToList();
        var chainWithBrokenDeserializer = new TimeRangePatchChain<EntityWithList>(
            patches,
            deserializer: _ =>
                throw new System.Text.Json.JsonException(
                    "The JSON value could not be converted to List`1"
                )
        );

        var act = () =>
            chainWithBrokenDeserializer.PatchToDate(initialEntity, keyDate + TimeSpan.FromDays(1));

        act.Should().ThrowExactly<System.Text.Json.JsonException>();
        chainWithBrokenDeserializer.FinalDeserializationFailed.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that when the final deserialization fails AND skip conditions are configured,
    /// the error is caught and the initial entity is returned.
    /// Also verifies that <see cref="TimeRangePatchChain{TEntity}.FinalDeserializationFailed"/> is set.
    /// </summary>
    [Fact]
    public void PatchToDate_With_SkipConditions_Returns_InitialEntity_When_Final_Deserialization_Fails()
    {
        // Build a valid chain first with the default deserializer
        var buildChain = new TimeRangePatchChain<EntityWithList>();
        var initialEntity = new EntityWithList
        {
            MyList = new List<ListItem> { new() { Value = "A" } },
        };
        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var updatedEntity = new EntityWithList
        {
            MyList = new List<ListItem>
            {
                new() { Value = "A" },
                new() { Value = "B" },
            },
        };
        buildChain.Add(initialEntity, updatedEntity, keyDate);

        // Extract raw patches and create a chain with a broken deserializer + skip condition
        var patches = buildChain.GetAll().ToList();
        var chainWithBrokenDeserializer = new TimeRangePatchChain<EntityWithList>(
            patches,
            deserializer: _ =>
                throw new System.Text.Json.JsonException(
                    "The JSON value could not be converted to List`1"
                ),
            skipConditions: new List<ISkipCondition<EntityWithList>>
            {
                new IgnoreAllSkipCondition(),
            }
        );

        var result = chainWithBrokenDeserializer.PatchToDate(
            initialEntity,
            keyDate + TimeSpan.FromDays(1)
        );

        result.Should().BeSameAs(initialEntity);
        chainWithBrokenDeserializer.PatchesHaveBeenSkipped.Should().BeTrue();
        chainWithBrokenDeserializer.FinalDeserializationFailed.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that <see cref="TimeRangePatchChain{TEntity}.FinalDeserializationFailed"/> is reset
    /// between calls to PatchToDate.
    /// </summary>
    [Fact]
    public void FinalDeserializationFailed_Is_Reset_Between_Calls()
    {
        var chain = new TimeRangePatchChain<EntityWithList>();
        var initialEntity = new EntityWithList
        {
            MyList = new List<ListItem> { new() { Value = "A" } },
        };
        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        chain.Add(
            initialEntity,
            new EntityWithList
            {
                MyList = new List<ListItem>
                {
                    new() { Value = "A" },
                    new() { Value = "B" },
                },
            },
            keyDate
        );

        // Normal patching should NOT set FinalDeserializationFailed
        var result = chain.PatchToDate(initialEntity, keyDate + TimeSpan.FromDays(1));
        chain.FinalDeserializationFailed.Should().BeFalse();
        result.MyList.Should().HaveCount(2);
    }

    /// <summary>
    /// A skip condition that always returns true for any error.
    /// This simulates IgnoreEverythingSkipCondition from downstream consumers.
    /// </summary>
    private class IgnoreAllSkipCondition : ISkipCondition<EntityWithList>
    {
        public bool ShouldSkipPatch(
            EntityWithList initialEntity,
            TimeRangePatch? failedPatch,
            Exception errorWhilePatching
        ) => true;
    }

    private static void ReverseAndRevert(
        TimeRangePatchChain<EntityWithList> chain,
        EntityWithList initialEntity
    )
    {
        var (reverseEntity, reverseChain) = chain.Reverse(initialEntity);
        var (rereverseEntity, rereverseChain) = reverseChain.Reverse(reverseEntity);
        rereverseChain.Should().BeEquivalentTo(chain);
        initialEntity.Should().BeEquivalentTo(rereverseEntity);
    }
}
