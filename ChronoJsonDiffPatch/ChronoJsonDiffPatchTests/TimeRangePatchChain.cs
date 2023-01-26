using ChronoJsonDiffPatch;
using FluentAssertions;

namespace ChronoJsonDiffPatchTests;

public class TimeRangePatchCollectionTests
{
    [Fact]
    public void Test_Contains()
    {
        var trpA = new TimeRangePatch(patch: null, from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero), to: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var trpB = new TimeRangePatch(patch: null, from: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var trpCollection = new TimeRangePatchChain(new[] { trpA, trpB });
        trpCollection.Contains(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        trpCollection.Contains(new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        trpCollection.Contains(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void Test_Contains_With_Grace()
    {
        var trpA = new TimeRangePatch(patch: null, from: new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero), to: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var trpB = new TimeRangePatch(patch: null, from: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var trpCollection = new TimeRangePatchChain(new[] { trpA, trpB });

        for (var i = -1000; i <= 1000; i += 100)
        {
            trpCollection.Contains(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(i)).Should().BeTrue();
            if (i != 0)
            {
                trpCollection.Contains(new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(i), graceTicks: 0).Should().BeFalse();
            }
        }
    }
}
