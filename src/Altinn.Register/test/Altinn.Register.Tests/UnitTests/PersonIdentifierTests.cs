#nullable enable

using System.Text.Json;
using Altinn.Register.Core.Parties;

namespace Altinn.Register.Tests.UnitTests;

public class PersonIdentifierTests 
{
    [Theory]
    [InlineData("01010100000")]
    [InlineData("00000000000")]
    [InlineData("12345678910")]
    public void ParsesValidPersonIdentifier(string identifier)
    {
        Assert.True(PersonIdentifier.TryParse(identifier, provider: null, out var result));
        result.Should().NotBeNull();
        result.ToString().Should().Be(identifier);

        var result2 = PersonIdentifier.Parse(identifier);
        result2.Should().NotBeNull();
        result2.ToString().Should().Be(identifier);

        Assert.True(PersonIdentifier.TryParse(identifier.AsSpan(), provider: null, out var result3));
        result3.Should().NotBeNull();
        result3.ToString().Should().Be(identifier);

        var result4 = PersonIdentifier.Parse(identifier.AsSpan());
        result4.Should().NotBeNull();
        result4.ToString().Should().Be(identifier);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456789101234")]
    [InlineData("1234ab78910")]
    [InlineData("123456-78910")]
    public void DoesNotParseInvalidPersonIdentifier(string identifier)
    {
        Assert.False(PersonIdentifier.TryParse(identifier, provider: null, out _));
        Assert.Throws<FormatException>(() => PersonIdentifier.Parse(identifier));
        Assert.False(PersonIdentifier.TryParse(identifier.AsSpan(), provider: null, out _));
        Assert.Throws<FormatException>(() => PersonIdentifier.Parse(identifier.AsSpan()));
    }

    [Fact]
    public void Equality()
    {
        var str1 = "12345678910";
        var str2 = "10987654321";
        var id1 = PersonIdentifier.Parse(str1);
        var id2 = PersonIdentifier.Parse(str2);

        #pragma warning disable CS1718 // Comparison made to same variable
        (str1 == id1).Should().BeTrue();
        (id1 == str1).Should().BeTrue();
        (id1 == id1).Should().BeTrue();
        id1.Equals(id1).Should().BeTrue();
        id1.Equals(str1).Should().BeTrue();
        id1.Equals((object)id1).Should().BeTrue();
        id1.Equals((object)str1).Should().BeTrue();

        (str2 == id1).Should().BeFalse();
        (id1 == str2).Should().BeFalse();
        (id1 == id2).Should().BeFalse();
        id1.Equals(id2).Should().BeFalse();
        id1.Equals(str2).Should().BeFalse();
        id1.Equals((object)id2).Should().BeFalse();
        id1.Equals((object)str2).Should().BeFalse();
#pragma warning restore CS1718 // Comparison made to same variable
    }

    [Fact]
    public void TryFormat()
    {
        var id = PersonIdentifier.Parse("12345678910");

        Span<char> span = stackalloc char[20];
        int written;

        // too short
        span.Fill(' ');
        id.TryFormat(span[..5], out written, [], provider: null).Should().BeFalse();

        // too long
        span.Fill(' ');
        id.TryFormat(span, out written, [], provider: null).Should().BeTrue();
        written.Should().Be(11);
        new string(span[..written]).Should().Be(id.ToString());

        // exact length
        span.Fill(' ');
        id.TryFormat(span[0..11], out written, [], provider: null).Should().BeTrue();
        written.Should().Be(11);
        new string(span[..written]).Should().Be(id.ToString());
    }

    [Fact]
    public void JsonRoundTrip()
    {
        var id = PersonIdentifier.Parse("12345678910");

        var json = JsonSerializer.SerializeToDocument(id);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.String);
        json.RootElement.GetString().Should().Be(id.ToString());

        var parsed = json.Deserialize<PersonIdentifier>();
        Assert.NotNull(parsed);

        parsed.Should().Be(id);
    }

    [Fact]
    public void BadJsonValue()
    {
        var json = "\"1234\"";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PersonIdentifier>(json));
    }
}
