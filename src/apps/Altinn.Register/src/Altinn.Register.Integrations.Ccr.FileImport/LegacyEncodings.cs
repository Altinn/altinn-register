using System.Text;

namespace Altinn.Register.Integrations.Ccr.FileImport;

/// <summary>
/// Provides access to legacy encodings that may be used in CCR files, such as Latin-9 (ISO-8859-15).
/// </summary>
internal static class LegacyEncodings
{
    static LegacyEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Latin9 = Encoding.GetEncoding("iso-8859-15");
    }

    /// <summary>
    /// Latin-9 (ISO-8859-15) encoding, which is commonly used in Norwegian CCR files.
    /// </summary>
    public static Encoding Latin9 { get; }
}
