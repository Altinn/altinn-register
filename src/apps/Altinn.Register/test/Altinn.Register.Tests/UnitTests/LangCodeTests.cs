#nullable enable

using System.Text.Json;
using Altinn.Register.Core.Parties;

namespace Altinn.Register.Tests.UnitTests;

public class LangCodeTests
{
    [Fact]
    public void FromCode_En_ReturnsEn()
    {
        LangCode code = LangCode.FromCode("en");
        
        code.Should().BeSameAs(LangCode.En);
    }

    [Fact]
    public void FromCode_Nb_ReturnsNb()
    {
        LangCode code = LangCode.FromCode("nb");

        code.Should().BeSameAs(LangCode.Nb);
    }

    [Fact]
    public void FromCode_Nn_ReturnsNn()
    {
        LangCode code = LangCode.FromCode("nn");
        
        code.Should().BeSameAs(LangCode.Nn);
    }

    [Fact]
    public void FromCode_Normalizes()
    {
        LangCode code = LangCode.FromCode("EN");

        code.Should().BeSameAs(LangCode.En);
    }

    [Fact]
    public void FromCode_Returns_SameInstance()
    {
        LangCode code1 = LangCode.FromCode("fr");
        LangCode code2 = LangCode.FromCode("FR");

        code1.Should().BeSameAs(code2);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("nb")]
    [InlineData("nn")]
    [InlineData("fr")]
    public void JsonRoundTrips_AsValue(string codeS)
    {
        LangCode code = LangCode.FromCode(codeS);

        var json = JsonSerializer.Serialize(code);

        var deserialized = JsonSerializer.Deserialize<LangCode>(json);

        deserialized.Should().BeSameAs(code);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("nb")]
    [InlineData("nn")]
    [InlineData("fr")]
    public void JsonRoundTrips_AsPropertyName(string codeS)
    {
        LangCode code = LangCode.FromCode(codeS);
        var dict = new Dictionary<LangCode, string>();
        dict[code] = code.Code;

        var json = JsonSerializer.Serialize(dict);

        var deserialized = JsonSerializer.Deserialize<Dictionary<LangCode, string>>(json);

        deserialized.Should().NotBeNull();
        deserialized.Should().ContainKey(code);
    }
}
