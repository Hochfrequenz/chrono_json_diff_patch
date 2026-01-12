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

### ORM-Friendly In-Place Entity Updates

When using this library with an ORM (like Entity Framework), you might encounter issues where the ORM loses track of your entities.
This happens because `PatchToDate()` creates a **new instance** of your entity via JSON deserialization.
The ORM tracks the original instance, so it sees the returned object as "new" rather than an update to the existing tracked entity.

To solve this, you can provide a `populateEntity` action that updates an existing entity in-place instead of creating a new one.

#### Using Newtonsoft.Json

```c#
using ChronoJsonDiffPatch;
using Newtonsoft.Json;

// Create a chain with populateEntity configured
var chain = new TimeRangePatchChain<Bicycle>(
    populateEntity: (json, entity) => JsonConvert.PopulateObject(json, entity)
);

// Add patches as usual
var initialBicycle = new Bicycle { Colour = "lila", MaxSpeedInKmh = 120 };
chain.Add(initialBicycle, new Bicycle { Colour = "brown", MaxSpeedInKmh = 120 }, colourChangeDate1);
chain.Add(initialBicycle, new Bicycle { Colour = "blue", MaxSpeedInKmh = 120 }, colourChangeDate2);

// This is your ORM-tracked entity
var ormTrackedBicycle = dbContext.Bicycles.Find(id);

// Use the three-argument overload to populate in-place
// The entity identity is preserved - ReferenceEquals(ormTrackedBicycle, originalRef) == true
chain.PatchToDate(initialBicycle, colourChangeDate2, ormTrackedBicycle);

// Now ormTrackedBicycle has the patched values, but it's the SAME instance
// Your ORM can properly track the changes
dbContext.SaveChanges(); // Works correctly - updates instead of insert
```

#### Using System.Text.Json (.NET 8+)

System.Text.Json doesn't have a direct `PopulateObject` equivalent, but you can achieve the same result using the `JsonObjectCreationHandling.Populate` feature with a `JsonTypeInfo` modifier. Here's a reusable generic helper method - define it once in your project and use it for any entity type:

```c#
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

// Define this helper method ONCE in your project - works for any entity type
public static class SystemTextJsonHelper
{
    public static void PopulateObject<T>(string json, T target) where T : class
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Type == typeof(T))
                        {
                            typeInfo.CreateObject = () => target;
                        }
                    }
                }
            },
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
        };
        JsonSerializer.Deserialize<T>(json, options);
    }
}
```

Then use it just like `JsonConvert.PopulateObject`:

```c#
using ChronoJsonDiffPatch;

// Create a chain - just as simple as the Newtonsoft.Json version
var chain = new TimeRangePatchChain<Bicycle>(
    populateEntity: SystemTextJsonHelper.PopulateObject
);

// Usage is the same
var ormTrackedBicycle = dbContext.Bicycles.Find(id);
chain.PatchToDate(initialBicycle, colourChangeDate2, ormTrackedBicycle);
dbContext.SaveChanges(); // Works correctly
```

The helper method is generic and works for any entity type with any number of properties - no per-property code needed.

The key differences from the legacy approach:
- Provide a `populateEntity` action in the constructor
- Use the three-argument `PatchToDate(initialEntity, keyDate, targetEntity)` overload
- The target entity is modified in-place, preserving object identity

#### Behavior with Nested Objects and Collections

Both Newtonsoft.Json and System.Text.Json (with `JsonObjectCreationHandling.Populate`) preserve references for nested objects and collections - they populate them **in-place** rather than replacing them. This is safe for ORM tracking of related entities:

```c#
class Order
{
    public string Status { get; set; }
    public Customer Customer { get; set; }      // Nested entity - reference preserved!
    public List<OrderItem> Items { get; set; }  // Collection - reference preserved!
}
```

**Important notes:**
- **Nested objects**: The reference is preserved and properties are updated in-place. Your ORM will continue tracking the nested entity.
- **Collections**: The reference is preserved, but items are **appended** (not replaced). If you need clean replacement semantics, consider using `[JsonIgnore]` on collection properties and handling them separately.
- **Use `[JsonIgnore]`** on navigation properties if you want to exclude them from patching entirely (e.g., when the ORM manages those relationships independently).

### Migrating from Legacy to ORM-Friendly Approach

If you're migrating existing code that stores entities at +infinity with patches, here's how to update:

**Before (legacy - creates new instances):**
```c#
var chain = new TimeRangePatchChain<MyEntity>(patches, PatchingDirection.AntiParallelWithTime);
var result = chain.PatchToDate(entityAtPlusInfinity, keyDate);
// result is a NEW instance - ORM loses tracking
```

**After (ORM-friendly - preserves identity):**
```c#
var chain = new TimeRangePatchChain<MyEntity>(
    patches,
    PatchingDirection.AntiParallelWithTime,
    populateEntity: (json, entity) => JsonConvert.PopulateObject(json, entity)
);

// Use your existing ORM-tracked entity as the target
chain.PatchToDate(entityAtPlusInfinity, keyDate, ormTrackedEntity);
// ormTrackedEntity is the SAME instance, just with updated properties
```

Note: The original `PatchToDate(initialEntity, keyDate)` method still works exactly as before for cases where you don't need identity preservation.

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


To create a **release** nuget package, create a tag of the form `vx.y.z` where `x.y.z` is the semantic version of the release. This will create and push nuget packages with the specified version `x.y.z`.
