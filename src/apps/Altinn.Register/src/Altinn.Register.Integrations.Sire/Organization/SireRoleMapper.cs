using CommunityToolkit.Diagnostics;

namespace Altinn.Register.Integrations.Sire.Organization;

/// <summary>
/// Maps SIRE <c>relasjonstype</c> values directly to Altinn role identifiers.
/// </summary>
/// <remarks>
/// Each SIRE <c>relasjonstype</c> corresponds to a Skattelisten (SL) code (e.g.
/// <c>"komplementar"</c> → <c>KOMP</c>), which in turn maps to a role identifier in the
/// <c>register.external_role_definition</c> table (e.g. <c>KOMP</c> → <c>"komplementar"</c>).
/// This mapper performs both steps in one go using the authoritative mapping defined in
/// <c>Persistence/Migration/v0.10-role-code/02-data-external-roles.sql</c> and
/// <c>Persistence/Migration/v0.18-role-identifiers/01-role-identifiers.sql</c>.
/// Source: Skatteetaten's kodeliste (relasjonstypeVirksomhet.xml, kodeliste-prod_v2).
/// </remarks>
public static class SireRoleMapper
{
    /// <summary>
    /// Maps a SIRE <c>relasjonstype</c> to the corresponding Altinn role identifier.
    /// </summary>
    /// <param name="relasjonstype">The technical name from SIRE (e.g. "komplementar").</param>
    /// <returns>The Altinn role identifier (e.g. "komplementar", "daglig-leder").</returns>
    /// <exception cref="ArgumentException">Thrown when the relasjonstype is not recognized.</exception>
    public static string GetRoleIdentifier(string relasjonstype) => relasjonstype switch
    {
        // SL-code shown in trailing comment for traceability against the kodeliste / SQL migration.
        "ErRevisoradresseFor"                 => "revisoradressat",                       // READ (kodeliste literally has capital E)
        "administrativEnhetOffentligSektor"   => "administrativ-enhet-offentlig-sektor",  // ADOS
        "bestyrendeReder"                     => "bestyrende-reder",                      // BEST
        "bostyrer"                            => "bostyrer",                              // BOBE
        "dagligLederAdministrerendeDirektoer" => "daglig-leder",                          // DAGL
        "deltakerMedProratariskAnsvar"        => "deltaker-delt-ansvar",                  // DTPR
        "deltakerMedSolidariskAnsvar"         => "deltaker-fullt-ansvar",                 // DTSO
        "denPersonligeKonkursenAngaar"        => "personlige-konkurs",                    // KENK
        "eierkommune"                         => "eierkommune",                           // EIKM
        "erBedriftTil"                        => "hovedenhet",                            // BEDR
        "erFrivilligRegistrertUtleiebyggFor"  => "utleiebygg",                            // UTBG
        "erRegnskapsfoereradresseFor"         => "regnskapsforeradressat",                // RFAD
        "erSaerskiltOppdeltEnhetTil"          => "saerskilt-oppdelt-enhet",               // OPMV
        "erVirksomhetDrevetIFellesskapAv"     => "virksomhet-fellesskap-drifter",         // VIFE
        "forestaarAvvikling"                  => "forestaar-avvikling",                   // AVKL
        "forretningsfoerer"                   => "forretningsforer",                      // FFOR
        "harSomDatterIKonsern"                => "konsern-datter",                        // KDAT
        "harSomGrunnlagForKonsern"            => "konsern-grunnlag",                      // KGRL
        "harSomMorIKonsern"                   => "konsern-mor",                           // KMOR
        "harSomRegistreringsenhet"            => "ikke-naeringsdrivende-hovedenhet",      // AAFY
        "helseforetak"                        => "helseforetak",                          // HLSE
        "innehaver"                           => "innehaver",                             // INNH
        "inngaarIFellesMvaRegistrering"       => "felles-registrert-med",                 // FEMV
        "inngaarIForetaksgruppeMed"           => "foretaksgruppe-med",                    // FGRP
        "inngaarIKirkeligFellesraad"          => "kirkelig-fellesraad",                   // KIRK
        "inngaarIKontorfellesskap"            => "kontorfelleskapmedlem",                 // KTRF
        "komplementar"                        => "komplementar",                          // KOMP
        "konkursdebitor"                      => "konkursdebitor",                        // KDEB
        "kontaktperson"                       => "kontaktperson",                         // KONT
        "lederIPartietsUtoevendeOrgan"        => "parti-organ-leder",                     // HLED
        "nestleder"                           => "nestleder",                             // NEST
        "nestlederIPartietsUtoevendeOrgan"    => "parti-organ-nestleder",                 // HNST
        "norskRepresentantForUtenlandskEnhet" => "norsk-representant",                    // REPR
        "observatoer"                         => "observator",                            // OBS
        "opplysningerOmForetaketIHjemlandet"  => "hovedforetak",                          // HFOR
        "organisasjonsleddIOffentligSektor"   => "organisasjonsledd-offentlig-sektor",    // ORGL
        "prokura"                             => "prokurist",                             // PROK
        "prokuraHverForSeg"                   => "prokurist-hver-for-seg",                // POHV
        "prokuraIFellesskap"                  => "prokurist-fellesskap",                  // POFE
        "regnskapsfoerer"                     => "regnskapsforer",                        // REGN
        "revisor"                             => "revisor",                               // REVI
        "sameiere"                            => "sameier",                               // SAM
        "signatur"                            => "signerer",                              // SIGN
        "signaturHverForSeg"                  => "signerer-hver-for-seg",                 // SIHV
        "signaturIFellesskap"                 => "signerer-fellesskap",                   // SIFE
        "skalFisjoneresMed"                   => "fisjonsovertaker",                      // FISJ
        "skalFusjoneresMed"                   => "fusjonsovertaker",                      // FUSJ
        "styremedlem"                         => "styremedlem",                           // MEDL
        "styremedlemIPartietsUtoevendeOrgan"  => "parti-organ-styremedlem",               // HMDL
        "styretsLeder"                        => "styreleder",                            // LEDE
        "varamedlem"                          => "varamedlem",                            // VARA
        "varamedlemIPartietsUtoevendeOrgan"   => "parti-organ-varamedlem",                // HVAR
        _ => ThrowHelper.ThrowArgumentException<string>(
            nameof(relasjonstype),
            $"Unknown SIRE relasjonstype: '{relasjonstype}'."),
    };
}
