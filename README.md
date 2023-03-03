# chrono_json_diff_patch

`ChronoJsonDiffPatch` is a .NET library that manages chronologically sorted JsonDiffPatches.
It combines [TimePeriodLibrary.NET](https://github.com/Giannoudis/TimePeriodLibrary) and [JsonDiffPatch.NET](https://github.com/wbish/jsondiffpatch.net).
It allows to describe the evolution of a JSON object over time as a `TimePeriodChain` of differential changes which are modelled as `JsonDiffPatch`es.

## Installation

Install it from nuget [ChronoJsonDiffPatch](https://www.nuget.org/packages/ChronoJsonDiffPatch):

```bash
dotnet add package ChronoJsonDiffPatch
```

| Version     | Number                                                                  |
| ----------- | ----------------------------------------------------------------------- |
| Stable      | ![Nuget Package](https://badgen.net/nuget/v/ChronoJsonDiffPatch)        |
| Pre-Release | ![Nuget Prerelease](https://badgen.net/nuget/v/ChronoJsonDiffPatch/pre) |

## Usage / Minimal Working Example

Assume there is a class that has a property:

```c#
using System.Text.Json.Serialization;

class Bicycle
    {
        [JsonPropertyName("colour")]
        public string Colour { get; set; }
        [JsonPropertyName("maxSpeedInKmh")]
        public int MaxSpeedInKmh { get; set; }
    }
```

The class has to be serializable as JSON (by default with `System.Text` but you can override with custom JSON (de)serializers).

Now you want to track changes of an instance of `Bicycle`.
Therefore, create a `TimeRangePatchChain<Bicycle>`:

```c#
using ChronoJsonDiffPatch;
// ...
var chain = new TimeRangePatchChain<Bicycle>();
var initialBicycle = new Bicycle // this is the state of the bicycle at beginning of time
{
    Colour = "lila",
    MaxSpeedInKmh = 120
};

var colourChangeDate1 = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);
var brownBicycle = new Bicycle
{
    Colour = "brown",
    MaxSpeedInKmh = 120
};

// adds the first two patches to the TimePeriodChain
chain.Add(initialBicycle, brownBicycle, colourChangeDate1);

var colourChangeDate2 = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
var blueBicycle = new Bicycle
{
    Colour = "blue",
    MaxSpeedInKmh = 120
};
// also track the changes at colourChangeDate2
chain.Add(initialBicycle, blueBicycle, colourChangeDate2);

// Now if you know the initial state of you bicycle + the chain,
// you can retrieve the state of the bicycle at any date, by applying the
// chronological patches to the initial state.
var arbitraryDateBeforeFirstColourChange = new DateTimeOffset(1995, 1, 1, 0, 0, 0, TimeSpan.Zero);
var stateAtBeforeFirstColourChange = chain.PatchToDate(initialBicycle, arbitraryDateBeforeFirstColourChange);
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

// note that if you use a gray cycle with lower max speed as initial entity, the result (with the same chain) looks different:
var anotherInitialBicycle = new Bicycle
{
    Colour = "gray",
    MaxSpeedInKmh = 25,
};
chain.PatchToDate(anotherInitialBicycle, DateTimeOffset.MinValue).Colour.Should().Be("gray");
chain.PatchToDate(anotherInitialBicycle, colourChangeDate2).Colour.Should().Be("blue");
chain.PatchToDate(anotherInitialBicycle, colourChangeDate2).MaxSpeedInKmh.Should().Be(25);
```

Find the full example in [`ShowCaseTests.cs`](ChronoJsonDiffPatch/ChronoJsonDiffPatchTests/ShowCaseTests.cs).

Internally the chain only saves the differential changes/JsonDiffPatches at the given dates:

| Index | Start               | End                 | JsonDiffPatch                 |
| ----- | ------------------- | ------------------- | ----------------------------- |
| 0     | `DateTime.MinValue` | `2022-01-01`        | `null`                        |
| 1     | `2022-01-01`        | `2023-01-01`        | `{"colour":["lila","brown"]}` |
| 2     | `2023-01-01`        | `DateTime.MaxValue` | `{"colour":["brown","blue"]}` |

In the end, the chain saves the changes to an entity as JsonDiffPatches without storing the entity itself.
This is useful if you handle large objects with `n` changes at certain moments and you don't want to to persist the majority of unchanged properties `n` times but only once.

### Patching Anti Parallel with Time

You can also model the entity such that the "base" of the patches is not the state at `DateTime.MinValue` but at `DateTime.MaxValue` and the patches model the differential changes from a future date towards the past.

```c#
// you can reverse any chain
var (stateAtPlusInfinity, reverseChain) = chain.Reverse(initialBicycle);
reverseChain.PatchingDirection.Should().Be(PatchingDirection.AntiParallelWithTime);
stateAtPlusInfinity.Colour.Should().Be("blue");
reverseChain.GetAll().Should().AllSatisfy(p => p.PatchingDirection.Should().Be(PatchingDirection.AntiParallelWithTime));
```

The patches then look like this:

| Index | Start               | End                 | JsonDiffPatch                 |
| ----- | ------------------- | ------------------- | ----------------------------- |
| 0     | `2023-01-01`        | `DateTime.MaxValue` | `null`                        |
| 1     | `2022-01-01`        | `2023-01-01`        | `{"colour":["brown","blue"]}` |
| 2     | `DateTime.MinValue` | `2022-01-01`        | `{"colour":["lila","brown"]}` |

## Code Quality / Production Readiness

- The code has [at least a 90%](https://github.com/Hochfrequenz/chrono_json_diff_patch/blob/main/.github/workflows/unittests_and_coverage.yml#L34) unit test coverage. ✔️
- The ChronoJsonDiffPatch package has no dependencies except for `TimePeriodLibrary.NET` and `JsonDiffPatch.NET`. ✔️

## Release Workflow

To create a **pre-release** nuget package, create a tag of the form `prerelease-vx.y.z` where `x.y.z` is the semantic version of the pre-release. This will create and push nuget packages with the specified version `x.y.z` and a `-betaYYYYMMDDHHmmss` suffix.

To create a **release** nuget package, create a tag of the form `vx.y.z` where `x.y.z` is the semantic version of the release. This will create and push nuget packages with the specified version `x.y.z`.
