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
    public void Builder_Count_ReturnsCorrectCount()
    {
        var builder = TranslatedText.CreateBuilder();

        builder.Should().HaveCount(0);

        builder.Add(LangCode.En, "hello");
        builder.Should().HaveCount(1);

        builder.Add(LangCode.FromCode("fr"), "bonjour");
        builder.Should().HaveCount(2);

        builder.Add(LangCode.Nb, "hei");
        builder.Should().HaveCount(3);

        builder.Remove(LangCode.En);
        builder.Should().HaveCount(2);

        builder.Remove(LangCode.FromCode("fr"));
        builder.Should().HaveCount(1);

        builder.Remove(LangCode.Nb);
        builder.Should().HaveCount(0);
    }

    [Fact]
    public void Builder_Keys_ReturnsAllKeys()
    {
        var builder = TranslatedText.CreateBuilder();
        var fr = LangCode.FromCode("fr");

        builder.Should().HaveCount(0);
        builder.ContainsKey(fr).Should().BeFalse();

        builder.Add(LangCode.En, "hello");
        builder.Keys.Should().Contain([LangCode.En]);
        builder.ContainsKey(LangCode.En).Should().BeTrue();

        builder.Add(fr, "bonjour");
        builder.Keys.Should().Contain([LangCode.En, fr]);
        builder.ContainsKey(fr).Should().BeTrue();

        builder.Add(LangCode.Nb, "hei");
        builder.Keys.Should().Contain([LangCode.En, fr, LangCode.Nb]);
        builder.ContainsKey(LangCode.Nb).Should().BeTrue();

        builder.Remove(LangCode.En);
        builder.Keys.Should().Contain([fr, LangCode.Nb]);
        builder.ContainsKey(LangCode.En).Should().BeFalse();

        builder.Remove(fr);
        builder.Keys.Should().Contain([LangCode.Nb]);
        builder.ContainsKey(fr).Should().BeFalse();

        builder.Remove(LangCode.Nb);
        builder.Keys.Should().BeEmpty();
        builder.ContainsKey(LangCode.Nb).Should().BeFalse();
    }

    [Fact]
    public void Builder_Values_ReturnsAllKeys()
    {
        var builder = TranslatedText.CreateBuilder();
        var fr = LangCode.FromCode("fr");

        builder.Should().HaveCount(0);

        builder.Add(LangCode.En, "hello");
        builder.Values.Should().Contain(["hello"]);

        builder.Add(fr, "bonjour");
        builder.Values.Should().Contain(["hello", "bonjour"]);

        builder.Add(LangCode.Nb, "hei");
        builder.Values.Should().Contain(["hello", "bonjour", "hei"]);

        builder.Remove(LangCode.En);
        builder.Values.Should().Contain(["bonjour", "hei"]);

        builder.Remove(fr);
        builder.Values.Should().Contain(["hei"]);

        builder.Remove(LangCode.Nb);
        builder.Values.Should().BeEmpty();
    }

    [Fact]
    public void Builder_Indexer_ThrowsOnMissingKey()
    {
        var builder = TranslatedText.CreateBuilder();
        
        Assert.Throws<KeyNotFoundException>(() => builder[LangCode.En]);
    }

    [Fact]
    public void Builder_Indexer_CanOverwrite()
    {
        var builder = TranslatedText.CreateBuilder();
        var fr = LangCode.FromCode("fr");

        builder[LangCode.En] = "1";
        builder[LangCode.En].Should().Be("1");

        builder[LangCode.En] = "2";
        builder[LangCode.En].Should().Be("2");

        builder[fr] = "3";
        builder[fr].Should().Be("3");

        builder[fr] = "4";
        builder[fr].Should().Be("4");
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
