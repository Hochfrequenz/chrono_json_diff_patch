using System.Text.Json.Serialization;
using AwesomeAssertions;
using ChronoJsonDiffPatch;

namespace ChronoJsonDiffPatchTests;

public class TimeRangePatchChainProductTests
{
    internal abstract class DummyClass
    {
        [JsonPropertyName("myProperty")]
        public string MyProperty { get; set; }
    }

    internal class DummyClassA : DummyClass { }

    internal class DummyClassB : DummyClass { }

    [Fact]
    public void Test_Joined_Chain()
    {
        var chainA = new TimeRangePatchChain<DummyClassA>();
        var initialEntityA = new DummyClassA { MyProperty = "initialA" };
        var chainB = new TimeRangePatchChain<DummyClassB>();
        var initialEntityB = new DummyClassB { MyProperty = "initialB" };
        {
            var datesAndValues = new Dictionary<DateTimeOffset, string>
            {
                { new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero), "2022A" },
                { new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero), "1990A" },
                { new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), "2027A" },
                { new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero), "2029A" },
                { new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), "2025A" },
            };
            foreach (var (patchDatetimeA, value) in datesAndValues)
            {
                var patchedEntityA = new DummyClassA { MyProperty = value };
                chainA.Add(
                    initialEntityA,
                    patchedEntityA,
                    patchDatetimeA,
                    FuturePatchBehaviour.KeepTheFuture
                );
            }
        }
        {
            var datesAndValues = new Dictionary<DateTimeOffset, string>
            {
                { new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero), "2023B" },
                { new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero), "1990B" },
                { new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "2026B" },
                { new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero), "2029B" },
                { new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), "2025B" },
            };
            foreach (var (patchDatetimeB, value) in datesAndValues)
            {
                var patchedEntityB = new DummyClassB { MyProperty = value };
                chainB.Add(
                    initialEntityB,
                    patchedEntityB,
                    patchDatetimeB,
                    FuturePatchBehaviour.KeepTheFuture
                );
            }
        }
        var product = new TimeRangePatchChainProduct<DummyClassA, DummyClassB>(
            chainA,
            initialEntityA,
            chainB,
            initialEntityB
        );
        var actual = product.GetAll().ToList();
        var expected = new List<ProductEntity<DummyClassA, DummyClassB>>
        {
            new(
                new DummyClassA { MyProperty = "1990A" },
                new DummyClassB { MyProperty = "1990B" },
                new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            new(
                new DummyClassA { MyProperty = "2022A" },
                new DummyClassB { MyProperty = "1990B" },
                new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            new(
                new DummyClassA { MyProperty = "2022A" },
                new DummyClassB { MyProperty = "2023B" },
                new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            new(
                new DummyClassA { MyProperty = "2025A" },
                new DummyClassB { MyProperty = "2025B" },
                new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            new(
                new DummyClassA { MyProperty = "2025A" },
                new DummyClassB { MyProperty = "2026B" },
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            new(
                new DummyClassA { MyProperty = "2027A" },
                new DummyClassB { MyProperty = "2026B" },
                new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
            new(
                new DummyClassA { MyProperty = "2029A" },
                new DummyClassB { MyProperty = "2029B" },
                new DateTimeOffset(2029, 1, 1, 0, 0, 0, TimeSpan.Zero)
            ),
        };
        actual.Should().BeEquivalentTo(expected);
    }
}
