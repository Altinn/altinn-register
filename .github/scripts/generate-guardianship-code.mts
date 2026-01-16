import {
  getGuardianships,
  type GuardianshipDefinition,
  type LocalizedString,
} from "./lib/guardianships.mts";
import { Trie, type TrieNode } from "./lib/trie.mts";

type GuardianshipArea = {
  readonly name: string;
  readonly identifier: string;
  tasks: Map<string, GuardianshipDefinition>;
  trie: Trie<GuardianshipDefinition>;

  readonly mapping: {
    readonly npr: string;
  };
};

const areas: Map<string, GuardianshipArea> = new Map();

for (const guardianship of await getGuardianships()) {
  let area = areas.get(guardianship.area);
  if (!area) {
    area = {
      name: guardianship.area,
      identifier: toIdentifier(guardianship.area),
      tasks: new Map(),
      trie: undefined!,

      mapping: {
        npr: guardianship.mapping.npr.virksomhet,
      },
    };

    areas.set(guardianship.area, area);
  }

  if (area.tasks.has(guardianship.task)) {
    throw new Error(
      `Duplicate guardianship for area="${guardianship.area}", task="${guardianship.task}"`
    );
  }

  if (area.mapping.npr !== guardianship.mapping.npr.virksomhet) {
    throw new Error(
      `Inconsistent npr mapping for area="${guardianship.area}": ` +
        `"${area.mapping.npr}" vs "${guardianship.mapping.npr.virksomhet}"`
    );
  }

  area.tasks.set(guardianship.task, guardianship);
}

const tries = Trie.from(
  [...areas.values()].map((area) => {
    area.trie = Trie.from(
      [...area.tasks.values()].map((g) => [g.mapping.npr.oppgave, g])
    );
    return [area.mapping.npr, area];
  })
);

function toIdentifier(name: string): string {
  name = name.toLowerCase().replace(/[-_ \/]+(.)/g, (_, c) => c.toUpperCase());
  return name.charAt(0).toUpperCase() + name.slice(1);
}

const INDENT = "    ";

function ind(line: string, indentation: string = INDENT): string {
  if (line.length === 0) return ``;
  else return `${indentation}${line}`;
}

function* indent(
  lines: Iterable<string>,
  indentation: string = INDENT
): Iterable<string> {
  for (const line of lines) {
    yield ind(line, indentation);
  }
}

function* areaRoleProperties(area: GuardianshipArea): Iterable<string> {
  let first = true;
  for (const [task, guardianship] of area.tasks) {
    if (first) first = false;
    else yield ``;

    yield `/// <summary>${guardianship.description.nb}</summary>`;
    yield `public static ExternalRoleReference ${toIdentifier(
      task
    )} { get; } = new(ExternalRoleSource.CivilRightsAuthority, "${
      guardianship.identifier
    }");`;
  }
}

function* areaRoleClasses(): Iterable<string> {
  let first = true;
  for (const area of areas.values()) {
    if (first) first = false;
    else yield ``;

    yield `/// <summary>Roles for guardianships in the area '${area.name}'.</summary>`;
    yield `public static partial class ${area.identifier}`;
    yield `{`;
    yield* indent(areaRoleProperties(area));
    yield `}`;
  }
}

function* trieMatch<T>(
  trie: TrieNode<T>,
  spanName: string,
  onMatch: (match: T) => Iterable<string>,
  utf8: boolean = false
): Iterable<string> {
  let index = 0;
  let firstLine = true;

  for (const child of trie) {
    if (firstLine) firstLine = false;
    else yield ``;

    const sliceName = `${spanName}_${index++}`;
    yield `if (${spanName}.StartsWith("${child.prefix}"${utf8 ? "u8" : ""}))`;
    yield `{`;

    yield ind(`var ${sliceName} = ${spanName}.Slice(${child.prefix.length});`);
    yield* indent(trieMatch(child, sliceName, onMatch, utf8));

    yield `}`;
  }

  const value = trie.value;
  if (typeof value !== "undefined") {
    if (firstLine) firstLine = false;
    else yield ``;

    yield `if (${spanName}.Length == 0)`;
    yield `{`;
    yield* indent(onMatch(value));
    yield `}`;
    yield `else`;
    yield `{`;
    yield ind(`goto end;`);
    yield `}`;
  }
}

function* tryFind(): Iterable<string> {
  // TryFindNprValue by UTF16 chars
  yield `/// <summary>Tries to find a guardianship role by NPR values.</summary>`;
  yield `/// <param name="vergeTjenestevirksomhet">The NPR value for the guardianship area.</param>`;
  yield `/// <param name="vergeTjenesteoppgave">The NPR value for the guardianship task.</param>`;
  yield `/// <param name="role">The found role, if any.</param>`;
  yield `/// <returns><see langword="true"/> if a role was found; otherwise, <see langword="false"/>.</returns>`;
  yield `public static bool TryFindRoleByNprValues(`;
  yield ind(`ReadOnlySpan<char> vergeTjenestevirksomhet,`);
  yield ind(`ReadOnlySpan<char> vergeTjenesteoppgave,`);
  yield ind(`[NotNullWhen(true)] out ExternalRoleReference? role)`);
  yield `{`;
  yield ind(`var s = vergeTjenestevirksomhet;`);
  yield* indent(
    trieMatch(tries, `s`, function* (match) {
      yield `return TryFind${match.identifier}RoleByNprValue(vergeTjenesteoppgave, out role);`;
    })
  );
  yield ``;
  yield ind(`end:`);
  yield ind(`role = null;`);
  yield ind(`return false;`);
  yield `}`;

  // TryFindNprValue by UTF8 bytes
  yield ``;
  yield `/// <summary>Tries to find a guardianship role by NPR values.</summary>`;
  yield `/// <param name="vergeTjenestevirksomhet">The NPR value for the guardianship area.</param>`;
  yield `/// <param name="vergeTjenesteoppgave">The NPR value for the guardianship task.</param>`;
  yield `/// <param name="role">The found role, if any.</param>`;
  yield `/// <returns><see langword="true"/> if a role was found; otherwise, <see langword="false"/>.</returns>`;
  yield `public static bool TryFindRoleByNprValues(`;
  yield ind(`ReadOnlySpan<byte> vergeTjenestevirksomhet,`);
  yield ind(`ReadOnlySpan<byte> vergeTjenesteoppgave,`);
  yield ind(`[NotNullWhen(true)] out ExternalRoleReference? role)`);
  yield `{`;
  yield ind(`var s = vergeTjenestevirksomhet;`);
  yield* indent(
    trieMatch(
      tries,
      `s`,
      function* (match) {
        yield `return TryFind${match.identifier}RoleByNprValue(vergeTjenesteoppgave, out role);`;
      },
      true
    )
  );
  yield ``;
  yield ind(`end:`);
  yield ind(`role = null;`);
  yield ind(`return false;`);
  yield `}`;

  for (const area of areas.values()) {
    // TryFindNprValue by UTF16 chars
    yield ``;
    yield `private static bool TryFind${area.identifier}RoleByNprValue(`;
    yield ind(`ReadOnlySpan<char> vergeTjenesteoppgave,`);
    yield ind(`[NotNullWhen(true)] out ExternalRoleReference? role)`);
    yield `{`;
    yield ind(`var s = vergeTjenesteoppgave;`);
    yield* indent(
      trieMatch(area.trie, `s`, function* (match) {
        yield `role = GuardianshipRoles.${
          area.identifier
        }.${toIdentifier(match.task)};`;
        yield `return true;`;
      })
    );
    yield ``;
    yield ind(`end:`);
    yield ind(`role = null;`);
    yield ind(`return false;`);
    yield `}`;

    // TryFindNprValue by UTF8 bytes
    yield ``;
    yield `private static bool TryFind${area.identifier}RoleByNprValue(`;
    yield ind(`ReadOnlySpan<byte> vergeTjenesteoppgave,`);
    yield ind(`[NotNullWhen(true)] out ExternalRoleReference? role)`);
    yield `{`;
    yield ind(`var s = vergeTjenesteoppgave;`);
    yield* indent(
      trieMatch(
        area.trie,
        `s`,
        function* (match) {
          yield `role = GuardianshipRoles.${area.identifier}.${toIdentifier(
            match.task
          )};`;
          yield `return true;`;
        },
        true
      )
    );
    yield ``;
    yield ind(`end:`);
    yield ind(`role = null;`);
    yield ind(`return false;`);
    yield `}`;
  }
}

const lines = [
  `// This file is autogenerated. Do not edit directly.`,
  `#nullable enable`,
  ``,
  `using Altinn.Register.Contracts;`,
  `using Altinn.Register.Contracts.ExternalRoles;`,
  `using System.Diagnostics.CodeAnalysis;`,
  ``,
  `namespace Altinn.Register.PartyImport.Npr;`,
  ``,
  `/// <summary>Roles for guardianships.</summary>`,
  `internal static partial class GuardianshipRoles`,
  `{`,
  ...indent(areaRoleClasses()),
  `}`,
  ``,
  `/// <summary>Mappings of guardianship values from npr to Altinn Register.</summary>`,
  `internal static partial class GuardianshipRoleMapper`,
  `{`,
  ...indent(tryFind()),
  `}`,
];

console.log(lines.join("\n"));
