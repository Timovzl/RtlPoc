using System.Runtime.CompilerServices;
using Rtl.News.RtlPoc.Application.Storage;
using Shouldly;

namespace Rtl.News.RtlPoc.Application.UnitTests.Storage;

public sealed class StorageOptionstests
{
    /// <summary>
    /// If a developer adds a property to the base class, this test helps them remember to add it to the subclass' copy constructor.
    /// </summary>
    [Fact]
    public void ConstructMultiReadOptions_WithReadOptions_ShouldCopyAllProperties()
    {
        var readOptions = new ReadOptions();

        var publicPropertiesWithInit = readOptions.GetType().GetProperties()
            .Where(property => property.GetSetMethod() is not null)
            .ToList();

        publicPropertiesWithInit.Count.ShouldBeGreaterThanOrEqualTo(2);

        foreach (var property in publicPropertiesWithInit)
        {
            var value = RuntimeHelpers.GetUninitializedObject(property.PropertyType);
            property.SetValue(readOptions, value);
        }

        var result = new MultiReadOptions(readOptions);

        foreach (var property in publicPropertiesWithInit)
        {
            var expectedValue = property.GetValue(readOptions);
            var value = property.GetValue(result);

            value.ShouldBe(expectedValue);
        }
    }
}
