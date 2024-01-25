using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Rtl.News.RtlPoc.Domain.UnitTests;

public sealed class DomainRegistrationExtensionsTests
{
    static DomainRegistrationExtensionsTests()
    {
        // We are testing JSON default config, which is static in nature
        var services = new ServiceCollection();
        services.AddDomainLayer(new ConfigurationBuilder().Build());
    }

    [Fact]
    public void AddDomainLayer_WhenSerializingJson_ShouldUseConfiguredMemberNames()
    {
        var instance = (UniqueKey)RuntimeHelpers.GetUninitializedObject(typeof(UniqueKey));

        var result = JsonConvert.SerializeObject(instance);

        result.ShouldContain(@"""Uniq_Path"":null");
        result.ShouldContain(@"""Uniq_Val"":null");
        result.ShouldNotContain(@"""Path""");
        result.ShouldNotContain(@"""Value""");
    }

    [Fact]
    public void AddDomainLayer_WhenDeserializingJson_ShouldUseConfiguredMemberNames()
    {
        var json = @"{ ""id"":""1"", ""Uniq_Path"":""/123"", ""Uniq_Val"":""123"" }";

        var result = JsonConvert.DeserializeObject<UniqueKey>(json);

        result.ShouldNotBeNull();
        result.Path.ShouldBe("/123");
        result.Value.ShouldBe("123");
    }
}
