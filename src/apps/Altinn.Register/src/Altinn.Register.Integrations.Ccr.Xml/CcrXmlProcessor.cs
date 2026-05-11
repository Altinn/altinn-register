using System.Buffers;
using System.Xml;
using Altinn.Authorization.ModelUtils;
using Altinn.Register.Contracts;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.ExternalRoles;
using Altinn.Register.Core.Location;
using Altinn.Register.Core.Parties;
using Altinn.Register.Core.Parties.Records;
using CommunityToolkit.Diagnostics;
using Nerdbank.Streams;

namespace Altinn.Register.Integrations.Ccr.Xml;

/// <summary>
/// XML processor for CCR (Customer Contact Register) data. This class provides functionality to read and process CCR XML data,
/// yielding updates as CcrPartyUpdate instances. The processing is done in an asynchronous manner, allowing for efficient handling of large XML data streams.
/// </summary>
public sealed class CcrXmlProcessor
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
    /// <param name="roleDef">Defines a lookup service for external role definitions, allowing retrieval of role definitions by source/identifier or role-code without asynchronous operations.</param>
    /// <param name="lookup">Gets static countrycode lookup</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of <see cref="CcrOrganizationUpdate"/> objects, each representing an update for a party found in
    /// the CCR XML. The stream is empty if no parties are present.</returns>
    public IEnumerable<CcrOrganizationUpdate> ProcessCcrXml(
        ReadOnlySequence<byte> xmlData,
        IExternalRoleDefinitionLookup roleDef,
        ILocationLookup lookup,
        CancellationToken cancellationToken = default)
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
            yield return ReadEnhet(reader, roleDef, lookup);
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

    private static CcrOrganizationUpdate ReadEnhet(
        XmlReader reader,
        IExternalRoleDefinitionLookup roleDef,
        ILocationLookup locationLookup)
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
                UnitStatus = hovedsakstype,
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
                    ReadSamendring(
                        reader,
                        nye: org.RoleUpdates.RoleAssignments,
                        fjernes: org.RoleUpdates.RemoveRoleAssignments,
                        roleDef,
                        locationLookup);
                }
                else if (reader.LocalName == "status")
                {
                    // ReadStatus(reader, org);
                }
                else if (reader.LocalName == "samendringUtgaar")
                {
                    org.RoleUpdates ??= new();
                    org.RoleUpdates.BulkRemoveRoleAssignments ??= [];
                    ReadSamu(
                        reader,
                        org.RoleUpdates.BulkRemoveRoleAssignments,
                        roleDef);
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

    private static void ReadInfoType(XmlReader reader, CcrOrganizationUpdate org)
    {
        var felttype = reader.GetAttribute("felttype") ?? string.Empty;
        var endringstype = reader.GetAttribute("endringstype") ?? string.Empty;

        switch (felttype, endringstype)
        {
            case ("EPOS", "N"):
                {
                    if (TryReadOpplysning(reader, out string? epost))
                    {
                        org.EmailAddress = !string.IsNullOrEmpty(epost) ? epost : FieldValue.Null;
                    }

                    break;
                }

            case ("EPOS", "U"):
                {
                    org.EmailAddress = FieldValue.Null;
                    break;
                }

            case ("FADR", "N"):
                {
                    var fadrFields = ReadChildFields(reader, "infotype");
                    org.BusinessAddress = ReadMailingAddress(fadrFields);
                    break;
                }

            case ("FADR", "U"):
                {
                    org.BusinessAddress = FieldValue.Null;
                    break;
                }

            case ("PADR", "N"):
                {
                    var fadrFields = ReadChildFields(reader, "infotype");
                    org.MailingAddress = ReadMailingAddress(fadrFields);
                    break;
                }

            case ("PADR", "U"):
                {
                    org.MailingAddress = FieldValue.Null;
                    break;
                }

            case ("IADR", "N"):
                {
                    if (TryReadOpplysning(reader, out string? iadr))
                    {
                        org.InternetAddress = !string.IsNullOrEmpty(iadr) ? iadr : FieldValue.Null;
                    }

                    break;
                }

            case ("IADR", "U"):
                {
                    org.InternetAddress = FieldValue.Null;
                    break;
                }

            case ("TFON", "N"):
                {
                    if (TryReadOpplysning(reader, out string? tlf))
                    {
                        org.TelephoneNumber = !string.IsNullOrEmpty(tlf) ? tlf : FieldValue.Null;
                    }

                    break;
                }

            case ("TFON", "U"):
                {
                    org.TelephoneNumber = FieldValue.Null;
                    break;
                }

            case ("MTLF", "N"):
                {
                    if (TryReadOpplysning(reader, out string? mtlf))
                    {
                        org.MobileNumber = !string.IsNullOrEmpty(mtlf) ? mtlf : FieldValue.Null;
                    }

                    break;
                }

            case ("MTLF", "U"):
                {
                    org.MobileNumber = FieldValue.Null;
                    break;
                }

            case ("TFAX", "N"):
                {
                    if (TryReadOpplysning(reader, out string? fax))
                    {
                        org.FaxNumber = !string.IsNullOrEmpty(fax) ? fax : FieldValue.Null;
                    }

                    break;
                }

            case ("TFAX", "U"):
                {
                    org.FaxNumber = FieldValue.Null;
                    break;
                }

            case ("NAVN", "N"):
                {
                    // We assume we dont get a redigertNavn for a full insert
                    var navnFields = ReadChildFields(reader, "infotype");
                    org.DisplayName = ReadAndConcatName(navnFields);
                    break;
                }

            case ("NAVN", "U"):
                {
                    org.DisplayName = FieldValue.Null;
                    break;
                }

            // The following felttyper are currently not mapped to any fields in the organization record,
            // but we want to allow them without throwing an exception, as they may be present in the XML data and we want to be able to process it without errors.
            // If we later decide to map any of these felttyper to fields in the organization record, we can simply add the necessary code to do so.
            case ("FMVA", "N" or "U"):
            case ("UREG", "N" or "U"):
            case ("ULOV", "N" or "U"):
            case ("PAAT", "N" or "U"):
            case ("NACE", "N" or "U"):
            case ("SN25", "N" or "U"):
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

    private static void ReadSamu(
        XmlReader reader,
        List<CcrRoleAssignment> samuLista,
        IExternalRoleDefinitionLookup roleDef)
    {
        var samuFields = ReadChildFields(reader, "samendringUtgaar");
        samuFields.TryGetValue("samendringstype", out var samuType);

        if (string.IsNullOrEmpty(samuType))
        {
            ThrowHelper.ThrowInvalidDataException("XmlReader: Missing required field 'samendringstype' in <samendringUtgaar> element.");
        }

        switch (samuType)
        {
            case "STYR":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("LEDE", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("NEST", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("MEDL", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("VARA", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("OBS", roleDef)));
                    break;
                }

            case "DELT":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("DTSO", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("DTPR", roleDef)));
                    break;
                }

            case "SIGN":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("SIGN", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("SIFE", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("SIHV", roleDef)));
                    break;
                }

            case "PROK":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("PROK", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("POHV", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("POFE", roleDef)));
                    break;
                }

            case "KONT":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("SREVA", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("KOMK", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("KNUF", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("KEMN", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("KONT", roleDef)));
                    break;
                }

            case "REVI":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("REVI", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("READ", roleDef)));
                    break;
                }

            case "REGN":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("RFAD", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("REGN", roleDef)));
                    break;
                }

            case "HOST":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("HLED", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("HMDL", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("HNST", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("HVAR", roleDef)));
                    break;
                }

            case "ESGR":
                {
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("ESGR", roleDef)));
                    samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode("ETDL", roleDef)));
                    break;
                }

            default:
                {
                    if (VerifyRoleCode(samuType))
                    {
                        samuLista.Add(CcrRoleAssignment.CreateBulkRoleAssignmentRemoval(ConvertToAltinnRoleCode(samuType, roleDef)));
                    }

                    break;
                }
        }

        ThrowHelper.ThrowInvalidDataException("XmlReader: unknown samendringstype '" + samuType + "' in <samendringUtgaar> element.");
    }

    private static bool VerifyRoleCode(string samuCode)
    {
        if (samuCode == "BEDR" || samuCode == "AAFY" || samuCode == "BEST" || samuCode == "BOBE" || samuCode == "DAGL"
                || samuCode == "KOMP" || samuCode == "REPR" || samuCode == "FFØR" || samuCode == "INNH" || samuCode == "ADOS"
                || samuCode == "AVKL" || samuCode == "EIKM" || samuCode == "FGRP" || samuCode == "HFOR" || samuCode == "HLSE"
                || samuCode == "KDAT" || samuCode == "KDEB" || samuCode == "KENK" || samuCode == "KGRL" || samuCode == "KIRK"
                || samuCode == "KMOR" || samuCode == "KOMP" || samuCode == "KTRF" || samuCode == "OPMV" || samuCode == "ORGL"
                || samuCode == "STFT" || samuCode == "UTBG" || samuCode == "VIFE")
        {
            return true;
        }

        return false;
    }

    // This method can be used to convert CCR role codes to Altinn role codes if they differ.
    // For example, if CCR uses "LEDER" and Altinn uses "LEDE", you could implement the mapping here.
    // For now, we assume they are the same.
    // In this case the feltype corresponds to a CCR role code, and we want to validate to the corresponding Altinn Role Code.
    private static string ConvertToAltinnRoleCode(
        string ccrRoleCode,
        IExternalRoleDefinitionLookup roleDef)
    {
        if (!roleDef.TryGetRoleDefinitionByRoleCode(ccrRoleCode, out var roleDefinition))
        {
            ThrowHelper.ThrowInvalidDataException($"XmlReader: Unknown role code '{ccrRoleCode}' in <samendringer> element. No corresponding role definition found.");
        }

        if (roleDefinition.Source != ExternalRoleSource.CentralCoordinatingRegister)
        {
            ThrowHelper.ThrowInvalidDataException($"XmlReader: Role code '{ccrRoleCode}' in <samendringer> element does not correspond to a role definition with source '{ExternalRoleSource.CentralCoordinatingRegister}'. Found role definition {roleDefinition.Identifier} with source '{roleDefinition.Source}'.");
        }

        return roleDefinition.Identifier;
    }

    private static void ReadSamendring(
        XmlReader reader,
        List<CcrRoleAssignment> nye,
        List<CcrRoleAssignment> fjernes,
        IExternalRoleDefinitionLookup roleDef,
        ILocationLookup locationLookup)
    {
        var felttype = reader.GetAttribute("felttype") ?? string.Empty;
        var endringstype = reader.GetAttribute("endringstype") ?? string.Empty;
        var type = reader.GetAttribute("type") ?? string.Empty;
        var data = reader.GetAttribute("data") ?? string.Empty;

        var rolleFields = ReadChildFields(reader, "samendringer");

        switch ((type, data))
        {
            case ("R", "D"):
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

                    // Convert CCR role code to Altinn role code
                    string validatedAltinnRoleCode = ConvertToAltinnRoleCode(felttype, roleDef);

                    if (endringstype == "N")
                    {
                        nye.Add(
                            CcrRoleAssignment.CreatePersonalRoleAssignment(
                                validatedAltinnRoleCode,
                                validatedRolleFnr,
                                PersonName.Create(fornavn, mellomnavn, etternavn),
                                ReadRoleAddress(adr1, adr2, adr3, rlandkode, postnr, locationLookup)));
                    }
                    else if (!string.IsNullOrEmpty(rolleFratraadt) && rolleFratraadt != "N" && endringstype != "N")
                    {
                        fjernes.Add(
                            CcrRoleAssignment.CreatePersonalRoleAssignment(
                                validatedAltinnRoleCode,
                                validatedRolleFnr,
                                PersonName.Create(fornavn, mellomnavn, etternavn),
                                ReadRoleAddress(adr1, adr2, adr3, rlandkode, postnr, locationLookup)));
                    }

                    break;
                }

            case ("K", "D"):
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

                    break;
                }
        }

        ThrowHelper.ThrowArgumentException("XmlReader: unknown samendring type '" + type + "' in <samendringer> element.");
    }

    private static MailingAddressRecord ReadRoleAddress(
        string? adr1,
        string? adr2,
        string? adr3,
        string? land,
        string? postnr,
        ILocationLookup locationLookup)
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

        if (!string.IsNullOrEmpty(land) && locationLookup.TryGetCountry(land, out Country? countryCode))
        {
            addressLines.Add(countryCode.Name);
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

    private static bool TryReadOpplysning(XmlReader reader, out string? value)
    {
        reader.MoveToContent();

        if (reader.NodeType != XmlNodeType.Element && reader.LocalName != "opplysning")
        {
            value = default;
            return false;
        }

        value = Normalize(reader.ReadElementContentAsString());
        reader.ReadEndElement();
        return true;
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

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
