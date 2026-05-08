using System.Buffers;
using System.Xml;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.Location;
using Altinn.Register.Core.Parties.Records;
using CommunityToolkit.Diagnostics;
using Nerdbank.Streams;

namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// XML processor for CCR (Customer Contact Register) data. This class provides functionality to read and process CCR XML data,
/// yielding updates as CcrPartyUpdate instances. The processing is done in an asynchronous manner, allowing for efficient handling of large XML data streams.
/// </summary>
public sealed class CcrXmlProcessor(
    ILocationLookup lookup,
    IExternalRoleDefinitionPersistence roleDef)
    : ICcrXmlProcessor
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
    /// <returns>An asynchronous stream of <see cref="CcrOrganizationUpdate"/> objects, each representing an update for a party found in
    /// the CCR XML. The stream is empty if no parties are present.</returns>
    public IEnumerable<CcrOrganizationUpdate> ProcessCcrXml(ReadOnlySequence<byte> xmlData, CancellationToken cancellationToken = default)
    {
        using var reader = XmlReader.Create(xmlData.AsStream());
        int enhet = 0;
        CcrBatchTrailer? trailer = null;

        // 1. Read to root element <batchAjourholdXML>
        reader.MoveToContent();
        reader.ReadStartElement("batchAjourholdXML");

        // 2. Read header <head ... />
        reader.MoveToContent();
        var header = ReadHeader(reader);

        // 3. Read <enhet> nodes
        reader.MoveToContent();
        while (reader.NodeType == XmlNodeType.Element && reader.LocalName == "enhet")
        {
            enhet++;
            cancellationToken.ThrowIfCancellationRequested();
            yield return ReadEnhet(reader);
            reader.MoveToContent();
        }

        // 4. Read trailer <trai ... />
        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "trai")
        {
            trailer = ReadTrailer(reader);
        }

        if (enhet == 0)
        {
            ThrowHelper.ThrowInvalidDataException("XmlReader: No <enhet> elements found in the document.");
        }

        if (enhet != trailer?.AntallEnheter)
        {
            ThrowHelper.ThrowInvalidDataException($"XmlReader: The number of <enhet> elements read ({enhet}) does not match the 'antallEnheter' attribute in the trailer ({trailer?.AntallEnheter}).");
        }
    }

    private static CcrBatchHeader ReadHeader(XmlReader reader)
    {
        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "head")
        {
            ThrowHelper.ThrowInvalidDataException("XmlReader: Expected <head> element at the beginning of the document.");
        }

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

    private static CcrOrganizationUpdate ReadEnhet(XmlReader reader)
    {
        CcrOrganizationUpdate org;

        var organisasjonsnummer = reader.GetAttribute("organisasjonsnummer");
        if (string.IsNullOrEmpty(organisasjonsnummer))
        {
            ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required attribute 'organisasjonsnummer' in <enhet> element.");
        }

        var organisasjonsform = reader.GetAttribute("organisasjonsform");
        var hovedsakstype = reader.GetAttribute("hovedsakstype");
        var undersakstype = reader.GetAttribute("undersakstype");
        var foersteOverfoering = reader.GetAttribute("foersteOverfoering");
        var datoSistEndret = ParseDate(reader.GetAttribute("datoSistEndret"));
        var isDeleted = hovedsakstype == "S";

        // If a first transfer, fields are explicitly set to null if missing from xml
        if (!string.IsNullOrEmpty(foersteOverfoering) && foersteOverfoering == "J")
        {
            org = new()
            {
                IsFirstRegistration = true,
                OrganizationIdentifier = OrganizationIdentifier.Parse(organisasjonsnummer),
                UnitType = organisasjonsform,

                DatoSistEndret = FieldValue.From(datoSistEndret),
                DisplayName = FieldValue.Null,
                BusinessAddress = FieldValue.Null,
                MailingAddress = FieldValue.Null,
                FaxNumber = FieldValue.Null,
                InternetAddress = FieldValue.Null,
                MobileNumber = FieldValue.Null,
                TelephoneNumber = FieldValue.Null,
                EmailAddress = FieldValue.Null,

                DeletedAt = isDeleted ? datoSistEndret : null,
                IsDeleted = isDeleted,
                RoleUpdates = null
            };
        }

        // On updates we set fields to Unset, and leave them as is in the Register db
        else
        {
            org = new()
            {
                IsFirstRegistration = false,
                OrganizationIdentifier = OrganizationIdentifier.Parse(organisasjonsnummer),
                UnitType = organisasjonsform,

                DatoSistEndret = FieldValue.From(datoSistEndret),
                DisplayName = FieldValue.Unset,
                BusinessAddress = FieldValue.Unset,
                MailingAddress = FieldValue.Unset,
                FaxNumber = FieldValue.Unset,
                InternetAddress = FieldValue.Unset,
                MobileNumber = FieldValue.Unset,
                TelephoneNumber = FieldValue.Unset,
                EmailAddress = FieldValue.Unset,

                DeletedAt = isDeleted ? datoSistEndret : null,
                IsDeleted = isDeleted,
                RoleUpdates = null
            };
        }

        if (!reader.IsEmptyElement)
        {
            reader.ReadStartElement("enhet");
            reader.MoveToContent();

            while (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.LocalName == "infotype")
                {
                    ReadInfoType(reader, org);
                }
                else if (reader.LocalName == "samendringer")
                {
                    org.RoleUpdates ??= new();
                    org.RoleUpdates.RoleAssignments ??= [];
                    org.RoleUpdates.RemoveRoleAssignments ??= [];
                    ReadSamendring(reader, nye: org.RoleUpdates.RoleAssignments, fjernes: org.RoleUpdates.RemoveRoleAssignments);
                }
                else if (reader.LocalName == "status")
                {
                    ReadStatus(reader, org);
                }
                else if (reader.LocalName == "samendringUtgaar")
                {
                    org.RoleUpdates ??= new();
                    org.RoleUpdates.BulkRemoveRoleAssignments ??= [];
                    ReadSamu(reader, org.RoleUpdates.BulkRemoveRoleAssignments);
                }
                else
                {
                    ThrowHelper.ThrowInvalidDataException("XmlReader: unknown element <" + reader.LocalName + "> in <enhet> element.");
                }

                reader.MoveToContent();
            }

            reader.ReadEndElement(); // </enhet>
        }
        else
        {
            reader.Read();
        }

        return org;
    }

    private static void ReadStatus(XmlReader reader, CcrOrganizationUpdate org)
    {
        var felttype = reader.GetAttribute("felttype") ?? string.Empty;
        var endringstype = reader.GetAttribute("endringstype") ?? string.Empty;
        if (felttype is "KONK" && endringstype is "N")
        {
            var statusFields = ReadChildFields(reader, "status");
            if (statusFields.TryGetValue("kjennelsesdato", out var kdato))
            {
            }
        }

        org.UnitStatus = felttype;
    }

    private static void ReadInfoType(XmlReader reader, CcrOrganizationUpdate org)
    {
        var felttype = reader.GetAttribute("felttype") ?? string.Empty;
        var endringstype = reader.GetAttribute("endringstype") ?? string.Empty;

        switch (felttype)
        {
            case "EPOS":
                {
                    var eposFields = ReadChildFields(reader, "infotype");
                    if (eposFields.TryGetValue("opplysning", out var epost))
                    {
                        org.EmailAddress = epost;
                    }

                    break;
                }

            case "FADR":
                {
                    var fadrFields = ReadChildFields(reader, "infotype");
                    org.BusinessAddress = ReadMailingAddress(fadrFields);
                    break;
                }

            case "PADR":
                {
                    var fadrFields = ReadChildFields(reader, "infotype");
                    org.MailingAddress = ReadMailingAddress(fadrFields);
                    break;
                }

            case "IADR":
                {
                    var iadrFields = ReadChildFields(reader, "infotype");
                    org.InternetAddress = iadrFields.TryGetValue("opplysning", out var internetAddress) ? internetAddress : null;
                    break;
                }

            case "TFON":
                {
                    var mtlfFields = ReadChildFields(reader, "infotype");
                    org.TelephoneNumber = mtlfFields.TryGetValue("opplysning", out var telephone) ? telephone : null;
                    break;
                }

            case "MTLF":
                {
                    var mtlfFields = ReadChildFields(reader, "infotype");
                    org.MobileNumber = mtlfFields.TryGetValue("opplysning", out var telephone) ? telephone : null;
                    break;
                }

            case "FAX":
                {
                    var mtlfFields = ReadChildFields(reader, "infotype");
                    org.FaxNumber = mtlfFields.TryGetValue("opplysning", out var telephone) ? telephone : null;
                    break;
                }

            case "NAVN":
                {
                    // We assume we dont get a redigertNavn for a full insert
                    var navnFields = ReadChildFields(reader, "infotype");
                    org.DisplayName = ReadAndConcatName(navnFields);
                    break;
                }

            // The following felttyper are currently not mapped to any fields in the organization record,
            // but we want to allow them without throwing an exception, as they may be present in the XML data and we want to be able to process it without errors.
            // If we later decide to map any of these felttyper to fields in the organization record, we can simply add the necessary code to do so.
            case "FMVA":
            case "UREG":
            case "ULOV":
            case "PAAT":
            case "NACE":
            case "SN25":
                break;

            default:
                {
                    ThrowHelper.ThrowInvalidDataException("XmlReader: unknown felttype '" + felttype + "' in <infotype> element.");
                    break;
                }
        }
    }

    private static FieldValue<string> ReadAndConcatName(Dictionary<string, string> navnFields)
    {
        string? line1 = navnFields.TryGetValue("navn1", out var l1) ? l1 : null;
        string? line2 = navnFields.TryGetValue("navn2", out var l2) ? l2 : null;
        string? line3 = navnFields.TryGetValue("navn3", out var l3) ? l3 : null;
        string? line4 = navnFields.TryGetValue("navn4", out var l4) ? l4 : null;
        string? line5 = navnFields.TryGetValue("navn5", out var l5) ? l5 : null;
        string name = string.Join(" ", new[] { line1, line2, line3, line4, line5 }.Where(s => !string.IsNullOrEmpty(s)));
        return name;
    }

    /// <summary>
    /// Used for organisations
    /// </summary>
    /// <param name="fadrFields">the fields from the xml</param>
    /// <returns></returns>
    private static MailingAddressRecord ReadMailingAddress(Dictionary<string, string> fadrFields)
    {
        string? line1 = fadrFields.TryGetValue("adresse1", out var l1) ? l1 : null;
        string? line2 = fadrFields.TryGetValue("adresse2", out var l2) ? l2 : null;
        string? line3 = fadrFields.TryGetValue("adresse3", out var l3) ? l3 : null;
        string? postkode = fadrFields.TryGetValue("postnr", out var pk) ? pk : null;
        string? poststed = fadrFields.TryGetValue("poststed", out var ps) ? ps : null;

        List<string> addressLines = [];
        if (!string.IsNullOrEmpty(line1))
        {
            addressLines.Add(line1);
        }

        if (!string.IsNullOrEmpty(line2))
        {
            addressLines.Add(line2);
        }

        if (!string.IsNullOrEmpty(line3))
        {
            addressLines.Add(line3);
        }

        if (addressLines.Count > 1 && postkode is not null && !addressLines[^1].StartsWith(postkode, StringComparison.Ordinal))
        {
            addressLines.Add($"{postkode} {poststed}".Trim());
        }

        string concatAddress = string.Join(" ", addressLines);

        return new MailingAddressRecord
        {
            Address = concatAddress,
            PostalCode = fadrFields.TryGetValue("postnr", out var postalCode) ? postalCode : null,
            City = fadrFields.TryGetValue("poststed", out var city) ? city : null,
        };
    }

    private static void ReadSamu(XmlReader reader, List<CcrRoleAssignment> samuLista)
    {
        var samuFields = ReadChildFields(reader, "samendringUtgaar");
        samuFields.TryGetValue("samendringstype", out var samuType);

        if (string.IsNullOrEmpty(samuType))
        {
            ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required field 'samendringstype' in <samendringUtgaar> element.");
        }

        if (samuType == "STYR")
        {
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("LEDE"));
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("NEST"));
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("MEDL"));
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("OBS"));
        }
        else if (samuType == "DELT")
        {
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("DTSO"));
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("DTPR"));
        }
        else if (samuType == "SIGN")
        {
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("SIGN"));
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("SIFE"));
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("SIHV"));
        }
        else if (samuType == "PROK")
        {
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("PROK"));
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("KENK"));
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval("KGRL"));
        }
        else
        {
            samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(samuType));
        }

        ThrowHelper.ThrowInvalidDataException("XmlReader: unknown samendringstype '" + samuType + "' in <samendringUtgaar> element.");
    }

    private static void ReadSamendring(XmlReader reader, List<CcrRoleAssignment> nye, List<CcrRoleAssignment> fjernes)
    {
        var felttype = reader.GetAttribute("felttype") ?? string.Empty;
        var endringstype = reader.GetAttribute("endringstype") ?? string.Empty;
        var type = reader.GetAttribute("type") ?? string.Empty;
        var data = reader.GetAttribute("data") ?? string.Empty;

        var rolleFields = ReadChildFields(reader, "samendringer");

        if (type == "R" && data == "D")
        {
            var rolleFoedselsnr = rolleFields.TryGetValue("rolleFoedselsnr", out var fod) ? fod : null;
            var rolleFratraadt = rolleFields.TryGetValue("rolleFratraadt", out var rfratr) ? rfratr : null;
            var fornavn = rolleFields.TryGetValue("fornavn", out var forn) ? forn : null;
            var mellomnavn = rolleFields.TryGetValue("mellomnavn", out var mell) ? mell : null;
            var etternavn = rolleFields.TryGetValue("slektsnavn", out var etter) ? etter : null;
            var postnr = rolleFields.TryGetValue("postnr", out var post) ? post : null;
            var adr1 = rolleFields.TryGetValue("adresse1", out var radr1) ? radr1 : null;
            var adr2 = rolleFields.TryGetValue("adresse2", out var radr2) ? radr2 : null;
            var adr3 = rolleFields.TryGetValue("adresse3", out var radr3) ? radr3 : null;
            var rlandkode = rolleFields.TryGetValue("adresseLandkode", out var rlk) ? rlk : null;

            var validatedRolleFnr = PersonIdentifier.TryParse(rolleFoedselsnr, null, out var fnr) ? fnr.ToString() : null;
            if (string.IsNullOrEmpty(validatedRolleFnr))
            {
                ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required field 'rolleFoedselsnr' for role assignment in <samendringer> element.");
            }

            if (endringstype == "N")
            {
                nye.Add(
                    CcrRoleAssignment.CreatePersonalRoleAssignment(
                        felttype,
                        validatedRolleFnr,
                        string.Join(" ", new[] { fornavn, mellomnavn }.Where(s => !string.IsNullOrEmpty(s))),
                        etternavn,
                        ReadRoleAddress(adr1, adr2, adr3, rlandkode, postnr)));
            }
            else if (!string.IsNullOrEmpty(rolleFratraadt) && rolleFratraadt != "N" && endringstype != "N")
            {
                fjernes.Add(
                    CcrRoleAssignment.CreatePersonalRoleAssignment(
                        felttype,
                        validatedRolleFnr,
                        string.Join(" ", new[] { fornavn, mellomnavn }.Where(s => !string.IsNullOrEmpty(s))),
                        etternavn,
                        ReadRoleAddress(adr1, adr2, adr3, rlandkode, postnr)));
            }
        }

        if (type == "K" && data == "D")
        {
            var knytningsOrgnr = rolleFields.TryGetValue("korrektOrganisasjonsnummer", out var kforn) ? kforn : null;
            var knytningsFratraadt = rolleFields.TryGetValue("knytningsFratraadt", out var kfratr) ? kfratr : null;

            if (string.IsNullOrEmpty(knytningsOrgnr))
            {
                ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required field 'korrektOrganisasjonsnummer' for organizational role assignment in <samendringer> element.");
            }

            var orgnr = OrganizationIdentifier.TryParse(knytningsOrgnr, null, out var temporg) ? temporg.ToString() : null;
            if (!string.IsNullOrEmpty(knytningsFratraadt) && knytningsFratraadt == "N")
            {
                nye.Add(CcrRoleAssignment.CreateConnection(felttype, orgnr));
            }
            else if (!string.IsNullOrEmpty(knytningsFratraadt) && knytningsFratraadt != "N")
            {
                fjernes.Add(CcrRoleAssignment.CreateConnection(felttype, orgnr));
            }
        }

        ThrowHelper.ThrowArgumentException("XmlReader: unknown samendring type '" + type + "' in <samendringer> element.");
    }

    private static MailingAddressRecord ReadRoleAddress(string? adr1, string? adr2, string? adr3, string? land, string? postnr)
    {
        List<string> addressLines = [];
        if (!string.IsNullOrEmpty(adr1))
        {
            addressLines.Add(adr1);
        }

        if (!string.IsNullOrEmpty(adr2))
        {
            addressLines.Add(adr2);
        }

        if (!string.IsNullOrEmpty(adr3))
        {
            addressLines.Add(adr3);
        }

        if (addressLines.Count > 1 && postnr is not null && !addressLines[^1].StartsWith(postnr, StringComparison.Ordinal))
        {
            addressLines.Add($"{postnr}".Trim());
        }

        string concatAddress = string.Join(" ", addressLines);
        return new MailingAddressRecord
        {
            Address = concatAddress,
            PostalCode = postnr is not null ? postnr : null,
            City = null,
        };
    }

    private static Dictionary<string, string> ReadChildFields(XmlReader reader, string parentElement)
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
