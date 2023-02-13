using System.Text.Json.Serialization;
using ChronoJsonDiffPatch;
using FluentAssertions;

namespace ChronoJsonDiffPatchTests;

public class ShowCaseTest
{
    class MyEntity
    {
        [JsonPropertyName("myProperty")]
        public string MyProperty { get; set; }
    }

    /// <summary>
    /// Apply to patches, in ascending order
    /// </summary>
    [Fact]
    public void Test_Three_Patches_Sequentially()
    {
        var chain = new TimeRangePatchChain<MyEntity>();
        var myEntityInitially = new MyEntity
        {
            MyProperty = "initial" // this is the state of the entity at beginning of time
        };

        var fooDate = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var myEntityFoo = new MyEntity
        {
            MyProperty = "foo"
        };

        // adds the first two patches to the TimePeriodChain
        chain.Add(myEntityInitially, myEntityFoo, fooDate);

        var barDate = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var myEntityBar = new MyEntity
        {
            MyProperty = "bar" // at barDate, MyProperty switches from "foo" to "bar" 
        };
        // also track the changes at barDate
        chain.Add(myEntityInitially, myEntityBar, barDate);

        // Now if you know the initial state of myEntity + the chain,
        // you can retrieve the state of myEntity at any date, by applying the 
        // chronological patches to the initial state.
        var anyDateBeforeFooDate = new DateTimeOffset(1995, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var stateBeforeFooDate = chain.PatchToDate(myEntityInitially, anyDateBeforeFooDate);
        stateBeforeFooDate.MyProperty.Should().Be("initial");

        // at fooDate, the state of the patched entity changes
        var stateAtFooDate = chain.PatchToDate(myEntityInitially, fooDate);
        stateAtFooDate.MyProperty.Should().Be("foo");

        // same goes for barDate
        var stateAtBarDate = chain.PatchToDate(myEntityInitially, barDate);
        stateAtBarDate.MyProperty.Should().Be("bar");
    }
}
