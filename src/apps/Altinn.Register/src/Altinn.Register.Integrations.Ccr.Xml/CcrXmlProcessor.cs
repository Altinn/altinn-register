using System.Buffers;
using System.Runtime.CompilerServices;
using System.Xml;
using Nerdbank.Streams;

namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// XML processor for CCR (Customer Contact Register) data. This class provides functionality to read and process CCR XML data,
/// yielding updates as CcrPartyUpdate instances. The processing is done in an asynchronous manner, allowing for efficient handling of large XML data streams.
/// </summary>
public sealed class CcrXmlProcessor
{
    /// <summary>
    /// Parses a CCR XML data stream and asynchronously yields updates for each party found in the document.
    /// </summary>
    /// <remarks>The caller is responsible for enumerating the returned asynchronous sequence. The method
    /// reads and processes the XML data in a forward-only, streaming manner, which allows for efficient handling of
    /// large documents. If the XML data is malformed or does not conform to the expected CCR structure, an exception
    /// may be thrown during enumeration.</remarks>
    /// <param name="xmlData">A read-only sequence of bytes containing the CCR XML data to process. The data must be a well-formed XML
    /// document in the expected CCR format.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of <see cref="CcrPartyUpdate"/> objects, each representing an update for a party found in
    /// the CCR XML. The stream is empty if no parties are present.</returns>
    public static async IAsyncEnumerable<CcrPartyUpdate> ProcessCcrXmlAsync(ReadOnlySequence<byte> xmlData, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = XmlReader.Create(xmlData.AsStream(), new XmlReaderSettings { Async = true });

        // 1. Read to root element <batchAjourholdXML>
        await reader.MoveToContentAsync();
        reader.ReadStartElement("batchAjourholdXML");

        // 2. Read header <head ... />
        await reader.MoveToContentAsync();
        var header = ReadHeader(reader);

        // 3. Read <enhet> nodes
        await reader.MoveToContentAsync();
        while (reader.NodeType == XmlNodeType.Element && reader.LocalName == "enhet")
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return ReadEnhet(reader);
            await reader.MoveToContentAsync();
        }

        // 4. Read trailer <trai ... />
        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "trai")
        {
            var trailer = ReadTrailer(reader);
        }
    }

    private static CcrBatchHeader ReadHeader(XmlReader reader)
    {
        var header = new CcrBatchHeader
        {
            Avsender = reader.GetAttribute("avsender") ?? string.Empty,
            Dato = reader.GetAttribute("dato") ?? string.Empty,
            Kjoerenr = reader.GetAttribute("kjoerenr") ?? string.Empty,
            Mottaker = reader.GetAttribute("mottaker") ?? string.Empty,
            Type = reader.GetAttribute("type") ?? string.Empty,
        };

        reader.Read(); // consume self-closing <head />
        return header;
    }

    private static CcrBatchTrailer ReadTrailer(XmlReader reader)
    {
        var trailer = new CcrBatchTrailer
        {
            AntallEnheter = int.TryParse(reader.GetAttribute("antallEnheter"), out var count) ? count : 0,
            Avsender = reader.GetAttribute("avsender") ?? string.Empty,
        };

        reader.Read(); // consume self-closing <trai />
        return trailer;
    }

    private static DateTimeOffset? ParseDate(string? value)
        => DateTimeOffset.TryParse(value, out var result) ? result : null;

    private static CcrPartyUpdate ReadEnhet(XmlReader reader)
    {
        var organisasjonsnummer = reader.GetAttribute("organisasjonsnummer") ?? string.Empty;
        var organisasjonsform = reader.GetAttribute("organisasjonsform") ?? string.Empty;
        var hovedsakstype = reader.GetAttribute("hovedsakstype") ?? string.Empty;
        var undersakstype = reader.GetAttribute("undersakstype") ?? string.Empty;
        var foersteOverfoering = ParseDate(reader.GetAttribute("foersteOverfoering"));
        var datoFoedt = ParseDate(reader.GetAttribute("datoFoedt"));
        var datoSistEndret = ParseDate(reader.GetAttribute("datoSistEndret"));

        var infotypes = new List<CcrInfoType>();
        var samendringer = new List<CcrSamendring>();

        if (!reader.IsEmptyElement)
        {
            reader.ReadStartElement("enhet");
            reader.MoveToContent();

            while (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.LocalName == "infotype")
                {
                    infotypes.Add(ReadInfoType(reader));
                }
                else if (reader.LocalName == "samendringer")
                {
                    samendringer.Add(ReadSamendring(reader));
                }
                else
                {
                    reader.Skip();
                }

                reader.MoveToContent();
            }

            reader.ReadEndElement(); // </enhet>
        }
        else
        {
            reader.Read();
        }

        return new CcrFullUpdate
        {
            Organisasjonsnummer = organisasjonsnummer,
            Organisasjonsform = organisasjonsform,
            Hovedsakstype = hovedsakstype,
            Undersakstype = undersakstype,
            FoersteOverfoering = foersteOverfoering ?? DateTimeOffset.MinValue,
            DatoFoedt = datoFoedt ?? DateTimeOffset.MinValue,
            DatoSistEndret = datoSistEndret ?? DateTimeOffset.MinValue,
            Infotypes = infotypes,
            Samendringer = samendringer,
        };
    }

    private static CcrInfoType ReadInfoType(XmlReader reader)
    {
        var felttype = reader.GetAttribute("felttype") ?? string.Empty;
        var endringstype = reader.GetAttribute("endringstype") ?? string.Empty;
        var fields = ReadChildFields(reader, "infotype");

        return new CcrInfoType
        {
            FeltType = felttype,
            EndringsType = endringstype,
            Fields = [.. fields.Select(kvp => new CcrField { FieldName = kvp.Key.ToFieldName(), Value = kvp.Value })],
        };
    }

    private static CcrSamendring ReadSamendring(XmlReader reader)
    {
        var felttype = reader.GetAttribute("felttype") ?? string.Empty;
        var endringstype = reader.GetAttribute("endringstype") ?? string.Empty;
        var type = reader.GetAttribute("type") ?? string.Empty;
        var data = reader.GetAttribute("data") ?? string.Empty;
        var fields = ReadChildFields(reader, "samendringer");

        return new CcrSamendring
        {
            FeltType = felttype,
            EndringsType = endringstype,
            Type = type,
            Data = data,
            Fields = [.. fields.Select(kvp => new CcrField { FieldName = kvp.Key.ToFieldName(), Value = kvp.Value })],
        };
    }

    private static IReadOnlyDictionary<string, string> ReadChildFields(XmlReader reader, string parentElement)
    {
        var fields = new Dictionary<string, string>();

        if (reader.IsEmptyElement)
        {
            reader.Read();
            return fields;
        }

        reader.ReadStartElement(parentElement);
        reader.MoveToContent();

        while (reader.NodeType == XmlNodeType.Element)
        {
            var name = reader.LocalName;
            var value = reader.ReadElementContentAsString();
            fields[name] = value;
            reader.MoveToContent();
        }

        reader.ReadEndElement();
        return fields;
    }
}
