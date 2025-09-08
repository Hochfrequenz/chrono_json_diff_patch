using System.Text.Json.Serialization;
using AwesomeAssertions;
using ChronoJsonDiffPatch;

namespace ChronoJsonDiffPatchTests;

public class ShowCaseTests
{
    class Bicycle
    {
        [JsonPropertyName("colour")]
        public string Colour { get; set; }

        [JsonPropertyName("maxSpeedInKmh")]
        public int MaxSpeedInKmh { get; set; }
    }

    /// <summary>
    /// Apply to patches, in ascending order
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Sequentially()
    {
        var chain = new TimeRangePatchChain<Bicycle>();
        var initialBicycle = new Bicycle // this is the state of the bicycle at beginning of time
        {
            Colour = "lila",
            MaxSpeedInKmh = 120,
        };

        var colourChangeDate1 = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var brownBicycle = new Bicycle { Colour = "brown", MaxSpeedInKmh = 120 };

        // adds the first two patches to the TimePeriodChain
        chain.Add(initialBicycle, brownBicycle, colourChangeDate1);

        var colourChangeDate2 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var blueBicycle = new Bicycle { Colour = "blue", MaxSpeedInKmh = 120 };
        // also track the changes at colourChangeDate2
        chain.Add(initialBicycle, blueBicycle, colourChangeDate2);

        // Now if you know the initial state of you bicycle + the chain,
        // you can retrieve the state of the bicycle at any date, by applying the
        // chronological patches to the initial state.
        var arbitraryDateBeforeFirstColourChange = new DateTimeOffset(
            1995,
            1,
            1,
            0,
            0,
            0,
            TimeSpan.Zero
        );
        var stateAtBeforeFirstColourChange = chain.PatchToDate(
            initialBicycle,
            arbitraryDateBeforeFirstColourChange
        );
        stateAtBeforeFirstColourChange.Colour.Should().Be("lila");
        stateAtBeforeFirstColourChange.MaxSpeedInKmh.Should().Be(120);

        // at colourChangeDate1, the state of the patched entity changes
        var stateAtFirstColourChange = chain.PatchToDate(initialBicycle, colourChangeDate1);
        stateAtFirstColourChange.Colour.Should().Be("brown");
        stateAtFirstColourChange.MaxSpeedInKmh.Should().Be(120);

        // same goes for colourChangeDate2
        var stateAtSecondColourChange = chain.PatchToDate(initialBicycle, colourChangeDate2);
        stateAtSecondColourChange.Colour.Should().Be("blue");
        stateAtSecondColourChange.MaxSpeedInKmh.Should().Be(120);

        // note that if you use a gray cycle with lower max speed as initial entity, the result looks different:
        var anotherInitialBicycle = new Bicycle { Colour = "gray", MaxSpeedInKmh = 25 };
        chain
            .PatchToDate(anotherInitialBicycle, DateTimeOffset.MinValue)
            .Colour.Should()
            .Be("gray");
        chain.PatchToDate(anotherInitialBicycle, colourChangeDate2).Colour.Should().Be("blue");
        chain.PatchToDate(anotherInitialBicycle, colourChangeDate2).MaxSpeedInKmh.Should().Be(25);

        // you can reverse any chain
        var (stateAtPlusInfinity, reverseChain) = chain.Reverse(initialBicycle);
        reverseChain.PatchingDirection.Should().Be(PatchingDirection.AntiParallelWithTime);
        stateAtPlusInfinity.Colour.Should().Be("blue");
        reverseChain
            .GetAll()
            .Should()
            .AllSatisfy(p =>
                p.PatchingDirection.Should().Be(PatchingDirection.AntiParallelWithTime)
            );
    }
}
