#nullable enable

using System.Text.Json;
using Altinn.Register.Core.Parties;

namespace Altinn.Register.Tests.UnitTests;

public class TranslatedTextTests
{
    [Fact]
    public void Building_WithMissingRequiredLanguages_Throws()
    {
        var builder = TranslatedText.CreateBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.ToImmutable());
    }

    [Fact]
    public void Building_WithRequiredLanguages_Works()
    {
        var builder = TranslatedText.CreateBuilder();

        builder.Add(LangCode.En, "hello");
        builder.Add(LangCode.Nb, "hei");
        builder.Add(LangCode.Nn, "hei");

        var text = builder.ToImmutable();

        text.En.Should().Be("hello");
        text.Nb.Should().Be("hei");
        text.Nn.Should().Be("hei");

        text[LangCode.En].Should().Be("hello");
        text[LangCode.Nb].Should().Be("hei");
        text[LangCode.Nn].Should().Be("hei");
    }

    [Fact]
    public void Builder_AddingSameLanguageTwice_Throws()
    {
        var builder = TranslatedText.CreateBuilder();

        builder.Add(LangCode.En, "hello");
        Assert.Throws<ArgumentException>(() => builder.Add(LangCode.En, "hello"));

        builder.Add(LangCode.FromCode("fr"), "bonjour");
        Assert.Throws<ArgumentException>(() => builder.Add(LangCode.FromCode("fr"), "bonjour"));
    }

    [Fact]
    public void JsonRoundtrip()
    {
        var builder = TranslatedText.CreateBuilder();

        builder.Add(LangCode.En, "hello");
        builder.Add(LangCode.Nb, "hei");
        builder.Add(LangCode.Nn, "hei");
        builder.Add(LangCode.FromCode("fr"), "bonjour");

        var text = builder.ToImmutable();

        var json = JsonSerializer.SerializeToDocument(text);
        json.RootElement.ValueKind.Should().Be(JsonValueKind.Object);

        var deserialized = JsonSerializer.Deserialize<TranslatedText>(json);

        deserialized.Should().BeEquivalentTo(text);
    }
}
