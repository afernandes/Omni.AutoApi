using Omni.AutoApi.Routing;
using Xunit;

namespace Omni.AutoApi.Tests;

public class TypeHelperTests
{
    private enum Color { Red, Green }

    [Theory]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(decimal), true)]
    [InlineData(typeof(System.Guid), true)]
    [InlineData(typeof(System.DateTime), true)]
    [InlineData(typeof(System.DateOnly), true)]
    [InlineData(typeof(System.TimeOnly), true)]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(System.DateTime?), true)]
    [InlineData(typeof(object), false)]
    [InlineData(typeof(int[]), false)]
    [InlineData(typeof(System.Collections.Generic.List<int>), false)]
    public void IsPrimitiveExtended_classifies_types(System.Type type, bool expected)
        => Assert.Equal(expected, TypeHelper.IsPrimitiveExtended(type, includeEnums: true));

    [Fact]
    public void Enum_is_simple_only_when_included()
    {
        Assert.True(TypeHelper.IsPrimitiveExtended(typeof(Color), includeEnums: true));
        Assert.False(TypeHelper.IsPrimitiveExtended(typeof(Color), includeEnums: false));
    }
}
