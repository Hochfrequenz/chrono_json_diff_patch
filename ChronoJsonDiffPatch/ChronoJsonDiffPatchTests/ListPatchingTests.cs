using System.Text.Json.Serialization;
using ChronoJsonDiffPatch;
using FluentAssertions;

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
