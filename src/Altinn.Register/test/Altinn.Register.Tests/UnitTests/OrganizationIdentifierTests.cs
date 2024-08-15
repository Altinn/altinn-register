#nullable enable

using System.Text.Json;
using Altinn.Register.Core.Parties;

namespace Altinn.Register.Tests.UnitTests;

public class OrganizationIdentifierTests
{
    [Theory]
    [InlineData("010101000")]
    [InlineData("000000000")]
    [InlineData("123456789")]
    public void ParsesValidOrganizationIdentifier(string identifier)
    {
        Assert.True(OrganizationIdentifier.TryParse(identifier, provider: null, out var result));
        result.Should().NotBeNull();
        result.ToString().Should().Be(identifier);

        var result2 = OrganizationIdentifier.Parse(identifier);
        result2.Should().NotBeNull();
        result2.ToString().Should().Be(identifier);

        Assert.True(OrganizationIdentifier.TryParse(identifier.AsSpan(), provider: null, out var result3));
        result3.Should().NotBeNull();
        result3.ToString().Should().Be(identifier);

        var result4 = OrganizationIdentifier.Parse(identifier.AsSpan());
        result4.Should().NotBeNull();
        result4.ToString().Should().Be(identifier);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456789101234")]
    [InlineData("1234ab789")]
    [InlineData("123456-789")]
    public void DoesNotParseInvalidPersonIdentifier(string identifier)
    {
        Assert.False(OrganizationIdentifier.TryParse(identifier, provider: null, out _));
        Assert.Throws<FormatException>(() => OrganizationIdentifier.Parse(identifier));
        Assert.False(OrganizationIdentifier.TryParse(identifier.AsSpan(), provider: null, out _));
        Assert.Throws<FormatException>(() => OrganizationIdentifier.Parse(identifier.AsSpan()));
    }

    [Fact]
    public void Equality()
    {
        var str1 = "123456789";
        var str2 = "109876543";
        var id1 = OrganizationIdentifier.Parse(str1);
        var id2 = OrganizationIdentifier.Parse(str2);

#pragma warning disable CS1718 // Comparison made to same variable
        (str1 == id1).Should().BeTrue();
        (id1 == str1).Should().BeTrue();
        (id1 == id1).Should().BeTrue();
        (str1 != id1).Should().BeFalse();
        (id1 != str1).Should().BeFalse();
        (id1 != id1).Should().BeFalse();
        id1.Equals(id1).Should().BeTrue();
        id1.Equals(str1).Should().BeTrue();
        id1.Equals((object)id1).Should().BeTrue();
        id1.Equals((object)str1).Should().BeTrue();

        (str2 == id1).Should().BeFalse();
        (id1 == str2).Should().BeFalse();
        (id1 == id2).Should().BeFalse();
        (str2 != id1).Should().BeTrue();
        (id1 != str2).Should().BeTrue();
        (id1 != id2).Should().BeTrue();
        id1.Equals(id2).Should().BeFalse();
        id1.Equals(str2).Should().BeFalse();
        id1.Equals((object)id2).Should().BeFalse();
        id1.Equals((object)str2).Should().BeFalse();
#pragma warning restore CS1718 // Comparison made to same variable
    }

    [Fact]
    public void TryFormat()
    {
        var id = OrganizationIdentifier.Parse("123456789");

        Span<char> span = stackalloc char[20];
        int written;

        // too short
        span.Fill(' ');
        id.TryFormat(span[..5], out written, [], provider: null).Should().BeFalse();

        // too long
        span.Fill(' ');
        id.TryFormat(span, out written, [], provider: null).Should().BeTrue();
        written.Should().Be(9);
        new string(span[..written]).Should().Be(id.ToString());

        // exact length
        span.Fill(' ');
        id.TryFormat(span[0..9], out written, [], provider: null).Should().BeTrue();
        written.Should().Be(9);
        new string(span[..written]).Should().Be(id.ToString());
    }

    [Fact]
    public void JsonRoundTrip()
    {
        var id = OrganizationIdentifier.Parse("123456789");

        var json = JsonSerializer.SerializeToDocument(id);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.String);
        json.RootElement.GetString().Should().Be(id.ToString());

        var parsed = json.Deserialize<OrganizationIdentifier>();
        Assert.NotNull(parsed);

        parsed.Should().Be(id);
    }

    [Fact]
    public void BadJsonValue()
    {
        var json = "\"1234\"";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OrganizationIdentifier>(json));
    }
}
