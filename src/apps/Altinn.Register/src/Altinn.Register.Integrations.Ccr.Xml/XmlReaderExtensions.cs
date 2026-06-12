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
    }
}
