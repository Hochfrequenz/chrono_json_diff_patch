using System.Text.Json.Serialization;
using AwesomeAssertions;
using ChronoJsonDiffPatch;
using Newtonsoft.Json;

namespace ChronoJsonDiffPatchTests;

/// <summary>
/// Twin tests for <see cref="TimeRangePatchChainTests"/> that verify the same behavior
/// but using the <c>populateEntity</c> approach for ORM-friendly in-place entity updates.
/// Each test verifies that:
/// 1. The patched values are correct (same as the original tests)
/// 2. The entity identity is preserved (ReferenceEquals returns true)
/// </summary>
public class PopulateEntityTests
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
    /// Creates a chain with populateEntity configured for in-place updates
    /// </summary>
    private static TimeRangePatchChain<T> CreateChainWithPopulate<T>(
        IEnumerable<TimeRangePatch>? timeperiods = null,
        PatchingDirection patchingDirection = PatchingDirection.ParallelWithTime
    )
    {
        return new TimeRangePatchChain<T>(
            timeperiods,
            patchingDirection,
            populateEntity: (json, entity) => JsonConvert.PopulateObject(json, entity)
        );
    }

    /// <summary>
    /// Helper to patch and verify identity is preserved
    /// </summary>
    private static void PatchToDateAndVerifyIdentity<T>(
        TimeRangePatchChain<T> chain,
        T initialEntity,
        DateTimeOffset keyDate,
        T targetEntity
    )
    {
        var originalReference = targetEntity;
        chain.PatchToDate(initialEntity, keyDate, targetEntity);
        ReferenceEquals(targetEntity, originalReference)
            .Should()
            .BeTrue("entity identity should be preserved after patching");
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Single_Patch"/>
    /// </summary>
    [Theory]
    [InlineData(-1, "foo")]
    [InlineData(0, "bar")]
    [InlineData(1, "bar")]
    public void Test_Single_Patch_WithPopulateEntity(int daysToKeyDate, string expectedProperty)
    {
        var trpCollection = CreateChainWithPopulate<DummyClass>();
        var myEntity = new DummyClass { MyProperty = "foo" };
        var myChangedEntity = new DummyClass { MyProperty = "bar" };
        var keyDate = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, myChangedEntity, keyDate);

        // Use populateEntity approach
        var targetEntity = new DummyClass { MyProperty = "original" };
        PatchToDateAndVerifyIdentity(
            trpCollection,
            myEntity,
            keyDate + TimeSpan.FromDays(daysToKeyDate),
            targetEntity
        );
        targetEntity.MyProperty.Should().Be(expectedProperty);
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Three_Patches_Sequentially"/>
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Sequentially_WithPopulateEntity()
    {
        var trpCollection = CreateChainWithPopulate<DummyClass>();
        var myEntity = new DummyClass { MyProperty = "foo" };

        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "bar" }, keyDateA);

        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "baz" }, keyDateB);

        // Test before patch
        var targetEntity = new DummyClass { MyProperty = "original" };
        PatchToDateAndVerifyIdentity(
            trpCollection,
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        targetEntity.MyProperty.Should().Be("foo");

        // Test at keyDateA
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateA, targetEntity);
        targetEntity.MyProperty.Should().Be("bar");

        // Test at keyDateB
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateB, targetEntity);
        targetEntity.MyProperty.Should().Be("baz");
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Three_Patches_Sequentially_with_the_last_one_being_at_the_same_date"/>
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Sequentially_SameDate_WithPopulateEntity()
    {
        var trpCollection = CreateChainWithPopulate<DummyClass>();
        var myEntity = new DummyClass { MyProperty = "foo" };

        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "bar" }, keyDateA);

        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "old-baz1" }, keyDateB);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "new-baz" }, keyDateB);

        var targetEntity = new DummyClass { MyProperty = "original" };

        // Before patch
        PatchToDateAndVerifyIdentity(
            trpCollection,
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        targetEntity.MyProperty.Should().Be("foo");

        // At keyDateA
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateA, targetEntity);
        targetEntity.MyProperty.Should().Be("bar");

        // At keyDateB
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateB, targetEntity);
        targetEntity.MyProperty.Should().Be("new-baz");
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Three_Patches_Sequentially_with_middle_last_one_being_at_the_same_date_keep_the_future"/>
    /// </summary>
    [Fact]
    public void Test_Three_Patches_KeepTheFuture_WithPopulateEntity()
    {
        var trpCollection = CreateChainWithPopulate<DummyClass>();
        var myEntity = new DummyClass { MyProperty = "foo" };

        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "bar1" }, keyDateA);

        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "baz" }, keyDateB);

        // Replace bar1 with bar2 at keyDateA, keeping future
        trpCollection.Add(
            myEntity,
            new DummyClass { MyProperty = "bar2" },
            keyDateA,
            FuturePatchBehaviour.KeepTheFuture
        );

        var targetEntity = new DummyClass { MyProperty = "original" };

        // Before patch
        PatchToDateAndVerifyIdentity(
            trpCollection,
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        targetEntity.MyProperty.Should().Be("foo");

        // At keyDateA
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateA, targetEntity);
        targetEntity.MyProperty.Should().Be("bar2");

        // At keyDateB - future is preserved
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateB, targetEntity);
        targetEntity.MyProperty.Should().Be("baz");
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Three_Patches_Sequentially_with_middle_last_one_being_at_the_same_date_overwrite_the_future"/>
    /// </summary>
    [Fact]
    public void Test_Three_Patches_OverwriteTheFuture_WithPopulateEntity()
    {
        var trpCollection = CreateChainWithPopulate<DummyClass>();
        var myEntity = new DummyClass { MyProperty = "foo" };

        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "bar1" }, keyDateA);

        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "baz" }, keyDateB);

        // Replace bar1 with bar2 at keyDateA, overwriting future
        trpCollection.Add(
            myEntity,
            new DummyClass { MyProperty = "bar2" },
            keyDateA,
            FuturePatchBehaviour.OverwriteTheFuture
        );

        var targetEntity = new DummyClass { MyProperty = "original" };

        // Before patch
        PatchToDateAndVerifyIdentity(
            trpCollection,
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        targetEntity.MyProperty.Should().Be("foo");

        // At keyDateA
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateA, targetEntity);
        targetEntity.MyProperty.Should().Be("bar2");

        // At keyDateB - future was overwritten
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateB, targetEntity);
        targetEntity.MyProperty.Should().Be("bar2");
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Three_Patches_Unordered"/>
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Unordered_WithPopulateEntity()
    {
        var trpCollection = CreateChainWithPopulate<DummyClass>();
        var myEntity = new DummyClass { MyProperty = "foo" };

        var keyDateB = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "baz" }, keyDateB);

        var keyDateA = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(
            myEntity,
            new DummyClass { MyProperty = "bar" },
            keyDateA,
            FuturePatchBehaviour.KeepTheFuture
        );

        var targetEntity = new DummyClass { MyProperty = "original" };

        // Before patch
        PatchToDateAndVerifyIdentity(
            trpCollection,
            myEntity,
            new DateTimeOffset(2011, 1, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        targetEntity.MyProperty.Should().Be("foo");

        // At keyDateA
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateA, targetEntity);
        targetEntity.MyProperty.Should().Be("bar");

        // At keyDateB
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateB, targetEntity);
        targetEntity.MyProperty.Should().Be("baz");
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Four_Patches_Unordered"/>
    /// </summary>
    [Fact]
    public void Test_Four_Patches_Unordered_WithPopulateEntity()
    {
        var trpCollection = CreateChainWithPopulate<DummyClass>();
        var myEntity = new DummyClass { MyProperty = "A" };

        var keyDateD = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(myEntity, new DummyClass { MyProperty = "D" }, keyDateD);

        var keyDateC = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(
            myEntity,
            new DummyClass { MyProperty = "C" },
            keyDateC,
            FuturePatchBehaviour.KeepTheFuture
        );

        var keyDateB = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(
            myEntity,
            new DummyClass { MyProperty = "B" },
            keyDateB,
            FuturePatchBehaviour.KeepTheFuture
        );

        var targetEntity = new DummyClass { MyProperty = "original" };

        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateB, targetEntity);
        targetEntity.MyProperty.Should().Be("B");

        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateC, targetEntity);
        targetEntity.MyProperty.Should().Be("C");

        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDateD, targetEntity);
        targetEntity.MyProperty.Should().Be("D");
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Patching_Many_Dates"/>
    /// </summary>
    [Fact]
    public void Test_Patching_Many_Dates_WithPopulateEntity()
    {
        var chain = CreateChainWithPopulate<DummyClass>();
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
            chain.Add(
                initialEntity,
                new DummyClass { MyProperty = value },
                patchDatetime,
                FuturePatchBehaviour.KeepTheFuture
            );
        }

        var targetEntity = new DummyClass { MyProperty = "original" };

        // Before any patch
        PatchToDateAndVerifyIdentity(
            chain,
            initialEntity,
            new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero),
            targetEntity
        );
        targetEntity.MyProperty.Should().Be(initialEntity.MyProperty);

        // At each key date
        foreach (var (keyDate, expectedValue) in datesAndValues.Skip(1))
        {
            PatchToDateAndVerifyIdentity(chain, initialEntity, keyDate, targetEntity);
            targetEntity.MyProperty.Should().Be(expectedValue);
        }
    }

    /// <summary>
    /// Twin of <see cref="TimeRangePatchChainTests.Test_Patching_On_Same_Date"/>
    /// </summary>
    [Fact]
    public void Test_Patching_On_Same_Date_WithPopulateEntity()
    {
        var trpCollection = CreateChainWithPopulate<DummyClassWithTwoProperties>();
        var myEntity = new DummyClassWithTwoProperties { MyPropertyA = "A0", MyPropertyB = "B0" };

        var keyDate1 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        trpCollection.Add(
            myEntity,
            new DummyClassWithTwoProperties { MyPropertyA = "A1", MyPropertyB = "B1" },
            keyDate1
        );
        trpCollection.Add(
            myEntity,
            new DummyClassWithTwoProperties { MyPropertyA = "A1", MyPropertyB = "B2" },
            keyDate1,
            FuturePatchBehaviour.KeepTheFuture
        );

        var targetEntity = new DummyClassWithTwoProperties
        {
            MyPropertyA = "original",
            MyPropertyB = "original",
        };

        // At MinValue
        PatchToDateAndVerifyIdentity(
            trpCollection,
            myEntity,
            DateTimeOffset.MinValue,
            targetEntity
        );
        targetEntity.MyPropertyA.Should().Be("A0");
        targetEntity.MyPropertyB.Should().Be("B0");

        // At keyDate1
        PatchToDateAndVerifyIdentity(trpCollection, myEntity, keyDate1, targetEntity);
        targetEntity.MyPropertyA.Should().Be("A1");
        targetEntity.MyPropertyB.Should().Be("B2");
    }

    /// <summary>
    /// Twin of <see cref="ShowCaseTests.Test_Three_Patches_Sequentially"/> (the bicycle example)
    /// </summary>
    [Fact]
    public void Test_Bicycle_ShowCase_WithPopulateEntity()
    {
        var chain = CreateChainWithPopulate<Bicycle>();
        var initialBicycle = new Bicycle { Colour = "lila", MaxSpeedInKmh = 120 };

        var colourChangeDate1 = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        chain.Add(
            initialBicycle,
            new Bicycle { Colour = "brown", MaxSpeedInKmh = 120 },
            colourChangeDate1
        );

        var colourChangeDate2 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        chain.Add(
            initialBicycle,
            new Bicycle { Colour = "blue", MaxSpeedInKmh = 120 },
            colourChangeDate2
        );

        // This is the ORM-tracked entity
        var ormTrackedBicycle = new Bicycle { Colour = "original", MaxSpeedInKmh = 0 };
        var originalReference = ormTrackedBicycle;

        // Before first change
        chain.PatchToDate(
            initialBicycle,
            new DateTimeOffset(1995, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ormTrackedBicycle
        );
        ReferenceEquals(ormTrackedBicycle, originalReference).Should().BeTrue();
        ormTrackedBicycle.Colour.Should().Be("lila");
        ormTrackedBicycle.MaxSpeedInKmh.Should().Be(120);

        // At first change
        chain.PatchToDate(initialBicycle, colourChangeDate1, ormTrackedBicycle);
        ReferenceEquals(ormTrackedBicycle, originalReference).Should().BeTrue();
        ormTrackedBicycle.Colour.Should().Be("brown");
        ormTrackedBicycle.MaxSpeedInKmh.Should().Be(120);

        // At second change
        chain.PatchToDate(initialBicycle, colourChangeDate2, ormTrackedBicycle);
        ReferenceEquals(ormTrackedBicycle, originalReference).Should().BeTrue();
        ormTrackedBicycle.Colour.Should().Be("blue");
        ormTrackedBicycle.MaxSpeedInKmh.Should().Be(120);
    }

    /// <summary>
    /// Test reverse chain with populateEntity (AntiParallelWithTime)
    /// Twin of the reverse part of <see cref="ShowCaseTests.Test_Three_Patches_Sequentially"/>
    /// </summary>
    [Fact]
    public void Test_Reverse_Chain_WithPopulateEntity()
    {
        var forwardChain = CreateChainWithPopulate<Bicycle>();
        var initialBicycle = new Bicycle { Colour = "lila", MaxSpeedInKmh = 120 };

        var colourChangeDate1 = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        forwardChain.Add(
            initialBicycle,
            new Bicycle { Colour = "brown", MaxSpeedInKmh = 120 },
            colourChangeDate1
        );

        var colourChangeDate2 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        forwardChain.Add(
            initialBicycle,
            new Bicycle { Colour = "blue", MaxSpeedInKmh = 120 },
            colourChangeDate2
        );

        var (stateAtPlusInfinity, reversedChain) = forwardChain.Reverse(initialBicycle);
        stateAtPlusInfinity.Colour.Should().Be("blue");

        // Create a new chain with populateEntity for the reversed patches
        var antiParallelChain = new TimeRangePatchChain<Bicycle>(
            reversedChain.GetAll(),
            PatchingDirection.AntiParallelWithTime,
            populateEntity: (json, entity) => JsonConvert.PopulateObject(json, entity)
        );

        var ormTrackedBicycle = new Bicycle { Colour = "original", MaxSpeedInKmh = 0 };
        var originalReference = ormTrackedBicycle;

        // At +infinity (no unpatching needed)
        antiParallelChain.PatchToDate(stateAtPlusInfinity, colourChangeDate2, ormTrackedBicycle);
        ReferenceEquals(ormTrackedBicycle, originalReference).Should().BeTrue();
        ormTrackedBicycle.Colour.Should().Be("blue");

        // Before colourChangeDate2 (unpatch to brown)
        antiParallelChain.PatchToDate(
            stateAtPlusInfinity,
            colourChangeDate1 + TimeSpan.FromDays(1),
            ormTrackedBicycle
        );
        ReferenceEquals(ormTrackedBicycle, originalReference).Should().BeTrue();
        ormTrackedBicycle.Colour.Should().Be("brown");

        // Before colourChangeDate1 (unpatch to lila)
        antiParallelChain.PatchToDate(
            stateAtPlusInfinity,
            new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ormTrackedBicycle
        );
        ReferenceEquals(ormTrackedBicycle, originalReference).Should().BeTrue();
        ormTrackedBicycle.Colour.Should().Be("lila");
    }

    /// <summary>
    /// Demonstrates that populateEntity and regular PatchToDate produce equivalent results
    /// </summary>
    [Fact]
    public void Test_PopulateEntity_Produces_Same_Results_As_Regular_PatchToDate()
    {
        // Create two identical chains - one with populateEntity, one without
        var chainWithPopulate = CreateChainWithPopulate<DummyClassWithTwoProperties>();
        var chainWithoutPopulate = new TimeRangePatchChain<DummyClassWithTwoProperties>();

        var initialEntity = new DummyClassWithTwoProperties
        {
            MyPropertyA = "A0",
            MyPropertyB = "B0",
        };

        // Add same patches to both chains
        var dates = new[]
        {
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        for (int i = 0; i < dates.Length; i++)
        {
            var entity = new DummyClassWithTwoProperties
            {
                MyPropertyA = $"A{i + 1}",
                MyPropertyB = $"B{i + 1}",
            };
            chainWithPopulate.Add(
                initialEntity,
                entity,
                dates[i],
                i > 0 ? FuturePatchBehaviour.KeepTheFuture : null
            );
            chainWithoutPopulate.Add(
                initialEntity,
                entity,
                dates[i],
                i > 0 ? FuturePatchBehaviour.KeepTheFuture : null
            );
        }

        // Test at various dates
        var testDates = new[]
        {
            DateTimeOffset.MinValue,
            new DateTimeOffset(2019, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2020, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2021, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2022, 6, 1, 0, 0, 0, TimeSpan.Zero),
            DateTimeOffset.MaxValue,
        };

        foreach (var testDate in testDates)
        {
            var resultFromRegular = chainWithoutPopulate.PatchToDate(initialEntity, testDate);

            var targetEntity = new DummyClassWithTwoProperties
            {
                MyPropertyA = "X",
                MyPropertyB = "Y",
            };
            chainWithPopulate.PatchToDate(initialEntity, testDate, targetEntity);

            targetEntity
                .MyPropertyA.Should()
                .Be(resultFromRegular.MyPropertyA, $"at date {testDate:O}, PropertyA should match");
            targetEntity
                .MyPropertyB.Should()
                .Be(resultFromRegular.MyPropertyB, $"at date {testDate:O}, PropertyB should match");
        }
    }

    internal class Bicycle
    {
        [JsonPropertyName("colour")]
        public string Colour { get; set; }

        [JsonPropertyName("maxSpeedInKmh")]
        public int MaxSpeedInKmh { get; set; }
    }

    #region Nested Entity Tests - Demonstrating Navigation Property Behavior

    /// <summary>
    /// Represents a nested/related entity that might be ORM-tracked separately
    /// </summary>
    internal class Owner
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }
    }

    /// <summary>
    /// Entity with a navigation property (nested tracked entity)
    /// </summary>
    internal class BicycleWithOwner
    {
        [JsonPropertyName("colour")]
        public string Colour { get; set; }

        [JsonPropertyName("owner")]
        public Owner Owner { get; set; }
    }

    /// <summary>
    /// Entity with a collection navigation property
    /// </summary>
    internal class BicycleWithAccessories
    {
        [JsonPropertyName("colour")]
        public string Colour { get; set; }

        [JsonPropertyName("accessories")]
        public List<string> Accessories { get; set; } = new();
    }

    /// <summary>
    /// GOOD NEWS: Newtonsoft.Json's PopulateObject PRESERVES nested object references by default!
    /// It recursively populates nested objects in-place rather than replacing them.
    /// This is safe for ORM tracking of nested entities.
    /// </summary>
    [Fact]
    public void Test_PopulateEntity_Preserves_Nested_Object_References_With_Newtonsoft()
    {
        var chain = new TimeRangePatchChain<BicycleWithOwner>(
            populateEntity: (json, entity) => JsonConvert.PopulateObject(json, entity)
        );

        var initialOwner = new Owner { Name = "Alice", Age = 30 };
        var initialBicycle = new BicycleWithOwner { Colour = "red", Owner = initialOwner };

        var changedOwner = new Owner { Name = "Bob", Age = 25 };
        var changedBicycle = new BicycleWithOwner { Colour = "blue", Owner = changedOwner };

        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        chain.Add(initialBicycle, changedBicycle, keyDate);

        // Create target with its own Owner instance (simulating ORM-tracked nested entity)
        var ormTrackedOwner = new Owner { Name = "OriginalOwner", Age = 99 };
        var targetBicycle = new BicycleWithOwner { Colour = "original", Owner = ormTrackedOwner };
        var originalBicycleRef = targetBicycle;
        var originalOwnerRef = ormTrackedOwner;

        // Patch to the changed state
        chain.PatchToDate(initialBicycle, keyDate, targetBicycle);

        // The bicycle instance is preserved (good for ORM)
        ReferenceEquals(targetBicycle, originalBicycleRef).Should().BeTrue();
        targetBicycle.Colour.Should().Be("blue");

        // GOOD: Newtonsoft.Json's PopulateObject preserves the Owner reference!
        // It populates the existing object in-place instead of replacing it.
        ReferenceEquals(targetBicycle.Owner, originalOwnerRef)
            .Should()
            .BeTrue(
                "Newtonsoft.Json PopulateObject preserves nested object references - ORM tracking is safe!"
            );

        // The nested object's values are updated IN-PLACE
        targetBicycle.Owner.Name.Should().Be("Bob");
        targetBicycle.Owner.Age.Should().Be(25);

        // The original reference points to the same (now modified) object
        originalOwnerRef.Name.Should().Be("Bob");
        originalOwnerRef.Age.Should().Be(25);
    }

    /// <summary>
    /// GOOD NEWS: Newtonsoft.Json's PopulateObject also preserves collection references!
    /// It clears and repopulates the existing collection rather than replacing it.
    /// </summary>
    [Fact]
    public void Test_PopulateEntity_Preserves_Collection_References_With_Newtonsoft()
    {
        var chain = new TimeRangePatchChain<BicycleWithAccessories>(
            populateEntity: (json, entity) => JsonConvert.PopulateObject(json, entity)
        );

        var initialBicycle = new BicycleWithAccessories
        {
            Colour = "red",
            Accessories = new List<string> { "bell", "light" },
        };

        var changedBicycle = new BicycleWithAccessories
        {
            Colour = "blue",
            Accessories = new List<string> { "basket", "lock", "mirror" },
        };

        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        chain.Add(initialBicycle, changedBicycle, keyDate);

        // Create target with its own collection (simulating ORM-tracked collection)
        var ormTrackedAccessories = new List<string> { "original-item" };
        var targetBicycle = new BicycleWithAccessories
        {
            Colour = "original",
            Accessories = ormTrackedAccessories,
        };
        var originalCollectionRef = ormTrackedAccessories;

        chain.PatchToDate(initialBicycle, keyDate, targetBicycle);

        // GOOD: The collection reference is PRESERVED with Newtonsoft.Json
        ReferenceEquals(targetBicycle.Accessories, originalCollectionRef)
            .Should()
            .BeTrue(
                "Newtonsoft.Json PopulateObject preserves collection references - ORM tracking is safe!"
            );

        // NOTE: Newtonsoft.Json APPENDS to collections by default, not replaces!
        // The original item is still there, plus the new items
        targetBicycle.Accessories.Should().Contain("original-item");
        targetBicycle.Accessories.Should().Contain("basket");
        targetBicycle.Accessories.Should().Contain("lock");
        targetBicycle.Accessories.Should().Contain("mirror");
    }

    /// <summary>
    /// This test shows the SAFE pattern: use [JsonIgnore] on navigation properties
    /// to prevent them from being replaced during populate.
    /// </summary>
    [Fact]
    public void Test_PopulateEntity_Safe_Pattern_With_JsonIgnore()
    {
        var chain = new TimeRangePatchChain<BicycleWithIgnoredOwner>(
            populateEntity: (json, entity) => JsonConvert.PopulateObject(json, entity)
        );

        var initialBicycle = new BicycleWithIgnoredOwner
        {
            Colour = "red",
            Owner = new Owner { Name = "Alice", Age = 30 },
        };

        var changedBicycle = new BicycleWithIgnoredOwner
        {
            Colour = "blue",
            Owner = new Owner { Name = "Bob", Age = 25 }, // This will be ignored in the patch
        };

        var keyDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        chain.Add(initialBicycle, changedBicycle, keyDate);

        // Create target with ORM-tracked owner
        var ormTrackedOwner = new Owner { Name = "ORMTrackedOwner", Age = 99 };
        var targetBicycle = new BicycleWithIgnoredOwner
        {
            Colour = "original",
            Owner = ormTrackedOwner,
        };
        var originalOwnerRef = ormTrackedOwner;

        chain.PatchToDate(initialBicycle, keyDate, targetBicycle);

        // Scalar property is updated
        targetBicycle.Colour.Should().Be("blue");

        // Navigation property is PRESERVED because of [JsonIgnore]
        ReferenceEquals(targetBicycle.Owner, originalOwnerRef)
            .Should()
            .BeTrue("JsonIgnore prevents the navigation property from being replaced");

        // Owner values are unchanged
        targetBicycle.Owner.Name.Should().Be("ORMTrackedOwner");
        targetBicycle.Owner.Age.Should().Be(99);
    }

    /// <summary>
    /// Entity demonstrating the safe pattern: navigation properties marked with [JsonIgnore]
    /// </summary>
    internal class BicycleWithIgnoredOwner
    {
        [JsonPropertyName("colour")]
        public string Colour { get; set; }

        /// <summary>
        /// Mark navigation properties with [JsonIgnore] to prevent PopulateObject from replacing them.
        /// This preserves ORM tracking for related entities.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public Owner Owner { get; set; }
    }

    #endregion
}
