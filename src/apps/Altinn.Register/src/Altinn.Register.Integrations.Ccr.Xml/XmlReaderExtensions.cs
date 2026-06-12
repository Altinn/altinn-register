using System.Diagnostics;
using System.Xml;
using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// Extension methods for <see cref="XmlReader"/>.
/// </summary>
internal static class XmlReaderExtensions
{
    extension(XmlReader reader)
    {
        // assertion variants of Is* methods
        public void AssertStartElement(string localName)
        {
            if (!reader.IsStartElement(localName))
            {
                ThrowHelper.ThrowInvalidDataException($"XmlReader: Expected <{localName}> start element, but found {PrintCurrent(reader)}.");
            }
        }

        public void AssertEmptyElement()
        {
            if (!reader.IsEmptyElement)
            {
                ThrowHelper.ThrowInvalidDataException($"XmlReader: Expected self-closing element, but found {PrintCurrent(reader)}.");
            }
        }

        public XmlReader ReadSubtree(string expectedLocalName)
        {
            reader.MoveToContent();

            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != expectedLocalName || reader.NamespaceURI != string.Empty)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    ThrowHelper.ThrowInvalidDataException($"XmlReader: Expected <{expectedLocalName}> element, but found <{reader.LocalName}> in namespace '{reader.NamespaceURI}'.");
                }

                ThrowHelper.ThrowInvalidDataException($"XmlReader: Expected <{expectedLocalName}> element.");
            }

            return reader.ReadSubtree();
        }

        public T ParseElement<T>()
            where T : IXmlParsable<T>
        {
            reader.MoveToContent();
            Debug.Assert(reader.NodeType == XmlNodeType.Element);

            T result;

            {
                using var subtree = reader.ReadSubtree();

                subtree.Read(); // Position on the root element of the subtree
                result = T.ParseNode(subtree);

                // Validate that the entire element was consumed by the parser
                subtree.MoveToContent();
                Debug.Assert(subtree.EOF);
            }

            // Move the main reader past the end element of the parsed subtree
            reader.Read();
            return result;
        }
    }

    private static string PrintCurrent(XmlReader reader)
    {
        return reader.NodeType switch
        {
            XmlNodeType.Element => $"<{reader.LocalName}> element",
            XmlNodeType.EndElement => $"</{reader.LocalName}> end element",
            XmlNodeType.Text => $"text node",
            XmlNodeType.Whitespace => "whitespace node",
            XmlNodeType.SignificantWhitespace => "significant whitespace node",
            _ => $"{reader.NodeType} node"
        };
    }
}
