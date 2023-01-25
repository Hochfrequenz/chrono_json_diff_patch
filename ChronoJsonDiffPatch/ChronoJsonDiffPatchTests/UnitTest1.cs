using ChronoJsonDiffPatch;
using FluentAssertions;

namespace ChronoJsonDiffPatchTests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        Class1.ReturnFoo().Should().Be("Foo");
    }
}
