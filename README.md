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

class MyEntity
{
    [JsonPropertyName("myProperty")]
    public string MyProperty { get; set; }
}
```

The class has to be serializable as JSON (by default with `System.Text` but you can override with custom JSON (de)serializers).

Now you want to track changes to of instance of `MyEntity`.
Therefore, create a `TimeRangePatchChain<MyEntity>`:

```c#
using ChronoJsonDiffPatch;
// ...
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
```

Find the full example in [`ShowCaseTest.cs`](ChronoJsonDiffPatch/ChronoJsonDiffPatchTests/ShowCaseTest.cs).

Internally the chain only saves the differential changes/JsonDiffPatches at the given dates:

| Index | Start               | End                 | JsonDiffPatch                      |
| ----- | ------------------- | ------------------- | ---------------------------------- |
| 0     | `DateTime.MinValue` | `fooDate`           | `null`                             |
| 1     | `fooDate`           | `barDate`           | `{"myProperty":["initial","foo"]}` |
| 2     | `barDate`           | `DateTime.MaxValue` | `{"myProperty":["foo","bar"]}`     |

## Code Quality / Production Readiness

- The code has [at least a 90%](https://github.com/Hochfrequenz/chrono_json_diff_patch/blob/main/.github/workflows/unittests_and_coverage.yml#L34) unit test coverage. ✔️
- The ChronoJsonDiffPatch package has no dependencies except for `TimePeriodLibrary.NET` and `JsonDiffPatch.NET`. ✔️

## Release Workflow

To create a **pre-release** nuget package, create a tag of the form `prerelease-vx.y.z` where `x.y.z` is the semantic version of the pre-release. This will create and push nuget packages with the specified version `x.y.z` and a `-betaYYYYMMDDHHmmss` suffix.

To create a **release** nuget package, create a tag of the form `vx.y.z` where `x.y.z` is the semantic version of the release. This will create and push nuget packages with the specified version `x.y.z`.
