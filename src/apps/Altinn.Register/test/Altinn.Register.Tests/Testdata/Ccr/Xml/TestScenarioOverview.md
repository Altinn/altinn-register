# Test Scenarios — CCR XML

Each `ScenarioN.xml` is a `batchAjourholdXML` document (the format the CCR
flat-file processor emits per organization). The XML is derived from real
production samples; identifying data has been replaced with synthetic values
that follow the same validation rules as the real format:

- **Org numbers** are 9-digit Norwegian organisasjonsnummer with a valid mod-11
  check digit. They are picked from the `316289xxx` synthetic test range that
  the rest of the test corpus already uses.
- **Person SSNs** (when present) are 11-digit Norwegian fødselsnummer using
  the synthetic D-number / "Tenor" convention (day-of-month + 40), with valid
  K1/K2 control digits.
- **Names** (organization and person) are obviously fake test names.

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
