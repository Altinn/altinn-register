using System.Buffers;
using System.Xml;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.Parties.Records;
using CommunityToolkit.Diagnostics;
using Nerdbank.Streams;

namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// XML processor for CCR (Customer Contact Register) data. This class provides functionality to read and process CCR XML data,
/// yielding updates as CcrPartyUpdate instances. The processing is done in an asynchronous manner, allowing for efficient handling of large XML data streams.
/// </summary>
public sealed class CcrXmlProcessor : ICcrXmlProcessor
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

        var org = new CcrOrganizationUpdate
        {
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
            RoleUpdates = null,
        };

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
                    org.RoleUpdates.RoleAssignments.Add(ReadSamendring(reader));
                }
                else if (reader.LocalName == "status")
                {
                    ReadStatus(reader, org);
                }
                else if (reader.LocalName == "samendringUtgaar")
                {
                    org.RoleUpdates ??= new();
                    org.RoleUpdates.BulkRemoveRoleAssignments.Add(ReadSamu(reader));
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

    private static CcrRoleAssignment ReadSamu(XmlReader reader)
    {
        var samuFields = ReadChildFields(reader, "samendringUtgaar");
        samuFields.TryGetValue("samendringstype", out var samuType);

        if (string.IsNullOrEmpty(samuType))
        {
            ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required field 'samendringstype' in <samendringUtgaar> element.");
        }

        var samu = CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(samuType);
        return samu;
    }

    private static CcrRoleAssignment ReadSamendring(XmlReader reader)
    {
        var felttype = reader.GetAttribute("felttype") ?? string.Empty;
        var endringstype = reader.GetAttribute("endringstype") ?? string.Empty;
        var type = reader.GetAttribute("type") ?? string.Empty;
        var data = reader.GetAttribute("data") ?? string.Empty;

        var rolleFields = ReadChildFields(reader, "samendringer");

        var rolleAnsvarsandel = rolleFields.TryGetValue("rolleAnsvarsandel", out var ansv) ? ansv : null;
        var rolleFratraadt = rolleFields.TryGetValue("rolleFratraadt", out var fratr) ? fratr : null;
        var rolleValgtav = rolleFields.TryGetValue("rolleValgtav", out var valgt) ? valgt : null;
        var rolleRekkefoelge = rolleFields.TryGetValue("rolleRekkefoelge", out var rek) ? rek : null;
        var rolleFoedselsnr = rolleFields.TryGetValue("rolleFoedselsnr", out var fod) ? fod : null;
        var fornavn = rolleFields.TryGetValue("fornavn", out var forn) ? forn : null;
        var mellomnavn = rolleFields.TryGetValue("mellomnavn", out var mell) ? mell : null;
        var etternavn = rolleFields.TryGetValue("slektsnavn", out var etter) ? etter : null;
        var postnr = rolleFields.TryGetValue("postnr", out var post) ? post : null;
        var adr1 = rolleFields.TryGetValue("adresse1", out var radr1) ? radr1 : null;
        var adr2 = rolleFields.TryGetValue("adresse2", out var radr2) ? radr2 : null;
        var adr3 = rolleFields.TryGetValue("adresse3", out var radr3) ? radr3 : null;
        var rlandkode = rolleFields.TryGetValue("adresseLandkode", out var rlk) ? rlk : null;
        var personstatus = rolleFields.TryGetValue("personstatus", out var ps) ? ps : null;
        var location = rolleFields.TryGetValue("plassering", out var loc) ? loc : null;
        var fritekst = rolleFields.TryGetValue("samendringfritTekstlinje", out var ft) ? ft : null;
        var rollefritFoedselsnr = rolleFields.TryGetValue("rollefritFoedselsnr", out var rff) ? rff : null;
        var rollefritTekstlinje = rolleFields.TryGetValue("rollefritTekstlinje", out var rft) ? rft : null;

        var knytningsAnsvarsdel = rolleFields.TryGetValue("knytningsAnsvarsandel", out var kan) ? kan : null;
        var knytningsFratraadt = rolleFields.TryGetValue("knytningsFratraadt", out var kfratr) ? kfratr : null;
        var knytningsValgtav = rolleFields.TryGetValue("knytningsValgtav", out var kvalgt) ? kvalgt : null;
        var knytningsRekkefoelge = rolleFields.TryGetValue("knytningsRekkefoelge", out var krek) ? krek : null;
        var knytningsOrgnr = rolleFields.TryGetValue("korrektOrganisasjonsnummer", out var kforn) ? kforn : null;
        var knytningfritOrganisasjonsnummer = rolleFields.TryGetValue("knytningfritOrganisasjonsnummer", out var kforn2) ? kforn2 : null;
        var knytningfritTekstlinje = rolleFields.TryGetValue("knytningfritTekstlinje", out var kftl) ? kftl : null;
        var korrektOrganisasjonsnummer = rolleFields.TryGetValue("korrektOrganisasjonsnummer", out var korg) ? korg : null;

        if (type == "R")
        {
            var fnr = GetFnr(rolleFoedselsnr, rollefritFoedselsnr, rollefritTekstlinje);
            if (string.IsNullOrEmpty(fnr))
            {
                ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required field 'rolleFoedselsnr' for role assignment in <samendringer> element.");
            }

            var addRole = CcrRoleAssignment.CreatePersonalRoleAssignment(felttype, fnr);
            addRole.SetRoleAssignmentFields(
                rolleAnsvarsDel: rolleAnsvarsandel,
                rolleFratraadt: rolleFratraadt,
                rolleValgtav: rolleValgtav,
                rolleRekkefoelge: rolleRekkefoelge,
                rolleFoedselsnr: rolleFoedselsnr,
                fornavn: fornavn,
                mellomnavn: mellomnavn,
                slektsnavn: etternavn,
                postnr: postnr,
                adr1: adr1,
                adr2: adr2,
                adr3: adr3,
                location: location,
                freeTextLine: fritekst,
                freeTextRole: rollefritTekstlinje,
                countryCode: rlandkode,
                personStatus: personstatus);
            return addRole;
        }

        if (type == "K")
        {
            var orgnr = GetOrgNr(knytningsOrgnr, knytningfritOrganisasjonsnummer, korrektOrganisasjonsnummer, knytningfritTekstlinje);
            var addConnection = CcrRoleAssignment.CreateConnection(felttype, orgnr);
            addConnection.SetConnectionFields(
                knytningAnsvarsandel: knytningsAnsvarsdel,
                knytningFratraadt: knytningsFratraadt,
                knytningValgtav: knytningsValgtav,
                knytningRekkefoelge: knytningsRekkefoelge,
                knytningfritTekstLinje: knytningfritTekstlinje);
            return addConnection;
        }

        ThrowHelper.ThrowArgumentException("XmlReader: unknown samendring type '" + type + "' in <samendringer> element.");
        return null;
    }

    private static string? GetFnr(string? rolleFoedselsnr, string? rollefritFoedselsnr, string? rollefritTekstlinje)
    {
        if (ValidateFnr(rolleFoedselsnr))
        {
            return rolleFoedselsnr!;
        }

        if (ValidateFnr(rollefritFoedselsnr))
        {
            return rollefritFoedselsnr!;
        }

        if (ValidateFnr(rollefritTekstlinje))
        {
            return rollefritTekstlinje!;
        }

        ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required field 'rolleFoedselsnr' for role assignment in <samendringer> element.");
        return null;
    }

    private static bool ValidateFnr(string? rolleFoedselsnr)
    {
        return PersonIdentifier.TryParse(rolleFoedselsnr, null, out var _);
    }

    private static string GetOrgNr(string? knytningsOrgnr, string? knytningfritOrganisasjonsnummer, string? korrektOrganisasjonsnummer, string? knytningfritTekstlinje)
    {
        if (ValidateOrgNr(korrektOrganisasjonsnummer))
        {
            return korrektOrganisasjonsnummer!;
        }

        if (ValidateOrgNr(knytningsOrgnr))
        {
            return knytningsOrgnr!;
        }

        if (ValidateOrgNr(knytningfritOrganisasjonsnummer))
        {
            return knytningfritOrganisasjonsnummer!;
        }

        if (ValidateOrgNr(knytningfritTekstlinje))
        {
            return knytningfritTekstlinje!;
        }

        ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required organizational number for connection assignment in <samendringer> element.");
        return null;
    }

    private static bool ValidateOrgNr(string? orgNr)
    {
        return OrganizationIdentifier.TryParse(orgNr, null, out var _);
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
