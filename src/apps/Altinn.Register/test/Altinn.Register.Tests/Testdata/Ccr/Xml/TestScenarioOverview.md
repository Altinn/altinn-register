# Test Scenarios — CCR XML

Each `ScenarioN.xml` is a `batchAjourholdXML` document (the format the CCR
flat-file processor emits per organization). The XML is derived from real
production samples; identifying data has been replaced with synthetic values
that follow the same validation rules as the real format:

- **Org numbers** are 9-digit Norwegian organisasjonsnummer with a valid mod-11
  check digit. They are picked from the `316289xxx` synthetic test range that
  the rest of the test corpus already uses.
- **Person SSNs** (when present) are 11-digit Norwegian fødselsnummer using
  the Tenor-style synthetic convention: day-of-month is incremented by 40
  (so DD becomes 41–71), the month and 2-digit year are kept, the
  individual number is **fully randomized**, and K1/K2 are recomputed to be
  mod-11 valid.
- **Names** (organization and person) are obviously fake test names. When
  the original carried a multi-word `fornavn`, the synthetic name keeps a
  multi-word `fornavn` so that branch stays covered.
- **Addresses** are replaced with synthetic street names + neutral
  postcodes when they appear. The shared-vs-distinct address structure is
  preserved (e.g., if three of four persons in the original lived at the
  same address, three of four still share an address in the synthetic
  version).
- **Same person → same fake SSN** across all references within a scenario.
  Role-transition records (e.g., a person vacating MEDL while taking up
  LEDE in the same update) only stay coherent if the SSN matches across
  records.

## Scenario 1 — New accountant (regnskapsfører) registered for a NUF

**Change type:** New `REGN` connection (`samendringer data="D" felttype="REGN" endringstype="N" type="K"`)
— a previously-existing NUF gets a new accountant relation registered.

**Parties:**

| Role | Type | Identifier | Note |
| --- | --- | --- | --- |
| Subject organization | NUF (Norskregistrert utenlandsk foretak) | `316289177` | Founded 2013-03-19, not first transfer |
| New accountant | Organization (REGN connection) | `316289347` | `knytningFratraadt=N` (active) |

**No persons, names, or addresses are part of this scenario** — it is a pure
organization-to-organization (`type="K"`) connection update.

## Scenario 2 — Two new accountants (regnskapsfører) registered for an AS

**Change type:** Two new `REGN` connections in a single update
(`samendringer data="D" felttype="REGN" endringstype="N" type="K"`, twice) —
an existing AS gets two accountant relations registered at once. The first
connection carries `knytningRekkefoelge=1`; the second is emitted without an
explicit order, exercising the "missing `knytningRekkefoelge`" branch.

**Parties:**

| Role | Type | Identifier | Note |
| --- | --- | --- | --- |
| Subject organization | AS (Aksjeselskap) | `316289134` | Founded 2013-04-09, not first transfer |
| New accountant #1 | Organization (REGN connection) | `316289029` | `knytningRekkefoelge=1`, active |
| New accountant #2 | Organization (REGN connection) | `316289045` | No `knytningRekkefoelge` element, active |

**No persons, names, or addresses are part of this scenario** — pure
organization-to-organization (`type="K"`) connection updates.

## Scenario 3 — Board reorganization in an ESEK (eierseksjonssameie)

**Change type:** A mixed bag of `type="R"` (Rolle/person-role) `samendringer`
representing a board change in a condominium-owners' association: a new
chairperson takes over (with the previous chair stepping down), three
board-member positions turn over, and one current deputy is promoted to
full board member.

The eight `samendringer` records (in the original order) are:

| # | Felttype | endringstype | Subject | Notes |
| --- | --- | --- | --- | --- |
| 1 | `LEDE` | `N` (new) | Anne Testperson (`42095823496`) | New chair (`rolleRekkefoelge=1`) |
| 2 | `LEDE` | `U` (utgår) | SSN `68025934532` only | Outgoing chair, identifier-only record |
| 3 | `MEDL` | `U` | SSN `42095823496` only | Same person as #1 — vacates her old MEDL seat to become LEDE |
| 4 | `MEDL` | `N` | Ola Test Testperson (`47055812817`) | New member (`rolleRekkefoelge=3`); two-word `fornavn` |
| 5 | `MEDL` | `U` | SSN `50077345679` only | Outgoing member |
| 6 | `MEDL` | `N` | Kari Marit Testperson (`50086039122`) | New member, **no `rolleRekkefoelge`**; two-word `fornavn`; different address |
| 7 | `MEDL` | `N` | Per Testperson (`55017928731`) | New member (`rolleRekkefoelge=2`) |
| 8 | `VARA` | `U` | SSN `55017928731` only | Same person as #7 — vacates deputy seat to become MEDL |

**Subject organization:** ESEK (eierseksjonssameie / condominium owners'
association) `316289118`, founded 2013-04-13, not first transfer.

**Persons** (six unique individuals, two of whom appear in two records):

| # | Synthetic SSN | Synthetic name | Born (from SSN) | Roles in this update |
| --- | --- | --- | --- | --- |
| P1 | `42095823496` | Anne Testperson | 1958-09-02¹ | LEDE-N, MEDL-U (promotion to chair) |
| P2 | `68025934532` | _(name not supplied)_ | 1959-02-28¹ | LEDE-U |
| P3 | `47055812817` | Ola Test Testperson | 1958-05-07¹ | MEDL-N |
| P4 | `50077345679` | _(name not supplied)_ | 1973-07-10¹ | MEDL-U |
| P5 | `50086039122` | Kari Marit Testperson | 1960-08-10¹ | MEDL-N |
| P6 | `55017928731` | Per Testperson | 1979-01-15¹ | MEDL-N, VARA-U (promotion to member) |

¹ Day-of-month decoded from `DD - 40` (Tenor synthetic convention).

All persons have `personstatus="L"` (Levende/alive). All `N` records carry
`rolleFratraadt="N"` (the new role is currently active).

**Addresses** (preserving the original 3-share-1-distinct structure):

| Address | Postcode | Used by |
| --- | --- | --- |
| `Testveien 1` | `1234` | P1, P3, P6 (the ESEK's registered address) |
| `Testgata 14` | `5678` | P5 (lives elsewhere) |

**Coverage value:** mixes new and ending records; includes identifier-only
`U` records; the same SSN appears in two `samendringer` (a person changing
role within the same organization in the same update); covers
`rolleRekkefoelge` = 1, 2, 3 and one missing-rekkefoelge case; covers
two-word `fornavn`; covers two distinct addresses (three persons share the
same address, one lives elsewhere).

## Scenario 4 — VAT-register status flipped to "not registered" on an AS

**Change type:** A bare `<infotype>` element (no `<samendringer>` wrapper)
with `felttype="R-MV"` (REGMV — Registreringsstatus MVA-register / VAT
register registration status) and `endringstype="U"`. The carried value is
`<opplysning>N</opplysning>`, i.e. the org is no longer registered in the
VAT register.

**Parties:**

| Role | Type | Identifier | Note |
| --- | --- | --- | --- |
| Subject organization | AS (Aksjeselskap) | `316289215` | `undersakstype="EBTC"`, founded 2013-07-17, not first transfer |

No second org, no persons, no names, no addresses — purely a status flag
flipped on the subject org itself.

**Coverage value:** the first scenario in this corpus that exercises a
top-level `<infotype>` element (rather than `samendringer`), and the only
one that exercises the **R-MV / REGMV boolean-length-value branch** in
the parser (`INFOTYPE_VALUE_LENGTH_BOOL`, distinct from the
default-length simple infotypes used by `R-FV`, `R-FR`, `R-SR`, `RSKP`,
etc.). Also covers `endringstype="U"` at the top-level infotype layer.

## Scenario 5 — New industry code (NACE) registered for a BEDR

**Change type:** A bare `<infotype felttype="naeringskode" endringstype="N">`
on a sub-enterprise (BEDR), registering a NACE/næringskode of `68.320`
(real-estate management on contract) effective from `2013-01-01`,
flagged as not a hjelpeenhet (`<hjelpeenhet>N</hjelpeenhet>`).

**Parties:**

| Role | Type | Identifier | Note |
| --- | --- | --- | --- |
| Subject organization | BEDR (sub-enterprise / local unit) | `316289096` | `undersakstype="EBTC"`, founded 2013-08-14, not first transfer |

No second org, no persons, no names, no addresses. The NACE code itself
(`68.320`) is a public classification, not PII, so it is retained as-is.

**Coverage value:** the only scenario hitting the **dedicated
`naeringskode` branch** in the parser (`WriteNaeringskode`), which emits
three child elements (`<naeringskode>`, `<gyldighetsdato>`,
`<hjelpeenhet>`) — structurally distinct from the single-`<opplysning>`
output of Scenario 4's `R-MV` branch even though both scenarios share the
"bare `<infotype>` under `<enhet>`" outer shape. Also the only scenario
that exercises `organisasjonsform="BEDR"`.

## Scenario 6 — Two REGN connections on an AS, one already Fratraadt

**Change type:** Two `samendringer data="D" felttype="REGN" endringstype="N" type="K"`
on the same AS in one update. The first connection is registered with
`knytningFratraadt="F"` (already stepped down) and `knytningRekkefoelge=1`;
the second carries `knytningFratraadt="N"` (active) and no
`knytningRekkefoelge`.

**Parties:**

| Role | Type | Identifier | Note |
| --- | --- | --- | --- |
| Subject organization | AS | `316289525` | Founded 2021-01-25, `datoSistEndret` is one day earlier than `head/dato` |
| Accountant #1 | Organization (REGN connection) | `316289533` | `knytningFratraadt=F`, `knytningRekkefoelge=1` |
| Accountant #2 | Organization (REGN connection) | `316289644` | `knytningFratraadt=N`, no `knytningRekkefoelge` |

**Coverage value (marginal):** structurally near-identical to Scenario 2
(AS / two REGN K samendringer / rekkefoelge 1+missing). The only novel
field-value is `knytningFratraadt="F"` on the first connection. The
parser does not branch on this value, so this scenario primarily adds
snapshot/regression coverage rather than a new code path. Useful as a
"recorded transition" shape (existing accountant stepping down while a
new one is registered, in the same update).

## Scenario 7 — Minimal new business address (FADR) on an ENK

**Change type:** A bare `<infotype felttype="FADR" endringstype="N">`
registering a new forretningsadresse (business address) on an ENK
(enkeltpersonforetak / sole proprietorship). Only `adresse1` is
populated — `adresse2` and `adresse3` are absent.

**Parties:**

| Role | Type | Identifier | Note |
| --- | --- | --- | --- |
| Subject organization | ENK (Enkeltpersonforetak) | `316289479` | Founded 2023-06-27, `datoSistEndret` is two days earlier than `head/dato` |

**Address (synthetic, replaces a real one):**

| Field | Value |
| --- | --- |
| `postnr` | `5678` |
| `landkode` | `NO` |
| `kommunenr` | `4601` |
| `adresse1` | `Testgata 2` |
| `adresse2` | _(absent)_ |

**Coverage value:** shares the `WriteAddress` branch with Scenario 8, but
exercises a different sub-case of that branch — the **minimal-FADR
path** with `adresse2` absent, hitting the `WriteOptionalTextElementNode`
IsEmpty short-circuit for the secondary address line. Also the **only
scenario covering `organisasjonsform="ENK"`** (sole proprietorship). No
`c/o` indirection in `adresse1` (plain street address), complementing
Scenario 8's c/o pattern.

## Scenario 8 — New business address (FADR) on an ESEK, with c/o person

**Change type:** A bare `<infotype felttype="FADR" endringstype="N">`
registering a new forretningsadresse (business address) on an ESEK. The
address has both `<adresse1>` and `<adresse2>` populated, with the
`adresse1` line carrying a `c/o` reference to a contact person.

**Parties:**

| Role | Type | Identifier | Note |
| --- | --- | --- | --- |
| Subject organization | ESEK | `316289304` | Founded 2021-05-08, not first transfer |

**Address (synthetic, replaces a real one):**

| Field | Value |
| --- | --- |
| `postnr` | `1234` |
| `landkode` | `NO` |
| `kommunenr` | `0301` |
| `adresse1` | `c/o Test Testperson` |
| `adresse2` | `Testveien 29` |

The original `c/o` line carried a real person's name, replaced with
"Test Testperson"; street name and postcode/kommune were also synthesized.

**Coverage value:** the only scenario hitting the shared
`WriteAddress` branch (used for both `FADR` and `PADR`), exercising all
five address sub-elements (`postnr`, `landkode`, `kommunenr`, `adresse1`,
`adresse2` — only `adresse3`, `poststed` are absent). Also the only
scenario covering a `c/o` pattern embedded in `adresse1` and
`organisasjonsform="ESEK"` outside the person-role context of Scenario 3.

## Scenario 9 — Single REGN connection on an AS, marked Fratraadt

**Change type:** A single `samendringer data="D" felttype="REGN" endringstype="N" type="K"`
with `knytningFratraadt="F"` (the connection is registered as already
stepped down) and `knytningRekkefoelge=4`.

**Parties:**

| Role | Type | Identifier | Note |
| --- | --- | --- | --- |
| Subject organization | AS | `316289312` | Founded 2021-06-11, not first transfer |
| Accountant (REGN) | Organization (REGN connection) | `316289428` | `knytningFratraadt=F`, `knytningRekkefoelge=4` |

**Coverage value (marginal):** hits the same parser branch as Scenarios
1, 2 (REGN/`type="K"`) but is the only scenario combining all of:
single-samendring + AS form + `knytningFratraadt="F"` +
`knytningRekkefoelge=4`. No new code path; only field-value variation
relative to existing scenarios. Useful for snapshot/regression coverage
if a future change makes `knytningFratraadt` value-sensitive.
