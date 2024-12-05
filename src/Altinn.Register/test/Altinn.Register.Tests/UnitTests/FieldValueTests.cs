#nullable enable

using System.Text.Json;
using Altinn.Register.Core.Utils;

namespace Altinn.Register.Tests.UnitTests;

public class FieldValueTests
{
    private static readonly JsonSerializerOptions _options
        = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .WithFieldValueSupport();

    [Fact]
    public void Deserialize_Missing_DefaultsTo_Unset()
    {
        var json = """{}""";
        var value = JsonSerializer.Deserialize<SingleField>(json, _options);

        Assert.NotNull(value);
        value.Value.Should().BeUnset();
    }

    [Fact]
    public void Deserialize_Null_Creates_Null()
    {
        var json = """{"value":null}""";
        var value = JsonSerializer.Deserialize<SingleField>(json, _options);

        Assert.NotNull(value);
        value.Value.Should().BeNull();
    }

    [Fact]
    public void Deserialize_EmptyString_Creates_EmptyString()
    {
        var json = """{"value":""}""";
        var value = JsonSerializer.Deserialize<SingleField>(json, _options);

        Assert.NotNull(value);
        value.Value.Should().HaveValue().Which.Should().BeEmpty();
    }

    [Fact]
    public void Serialize_Unset_Skips()
    {
        var value = new SingleField { Value = FieldValue.Unset };
        var json = JsonSerializer.Serialize(value, _options);

        json.Should().Be("""{}""");
    }

    [Fact]
    public void Serialize_Null_Serializes()
    {
        var value = new SingleField { Value = null };
        var json = JsonSerializer.Serialize(value, _options);
        
        json.Should().Be("""{"value":null}""");
    }

    [Fact]
    public void Serialize_EmptyString_Serializes()
    {
        var value = new SingleField { Value = string.Empty };
        var json = JsonSerializer.Serialize(value, _options);
        
        json.Should().Be("""{"value":""}""");
    }

    public record SingleField
    {
        public FieldValue<string> Value { get; init; }
    }
}
