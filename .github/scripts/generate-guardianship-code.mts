import { getAreas, type GuardianshipArea } from "./lib/guardianships.mts";
import { type TrieNode } from "./lib/trie.mts";

const { map: areas, trie: tries } = await getAreas();

const INDENT = "    ";

function ind(line: string, indentation: string = INDENT): string {
  if (line.length === 0) return ``;
  else return `${indentation}${line}`;
}

function* indent(
  lines: Iterable<string>,
  indentation: string = INDENT,
): Iterable<string> {
  for (const line of lines) {
    yield ind(line, indentation);
  }
}

function* areaRoleProperties(area: GuardianshipArea): Iterable<string> {
  let first = true;
  for (const task of area.tasks.values()) {
    if (first) first = false;
    else yield ``;

    yield `/// <summary>${task.guardianship.description.nb}</summary>`;
    yield `public static ExternalRoleReference ${task.identifier} { get; } = new(ExternalRoleSource.CivilRightsAuthority, "${
      task.guardianship.identifier
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
  utf8: boolean = false,
  level: number = 0,
): Iterable<string> {
  let index = 0;
  let firstLine = true;

  if (trie.branches > 1) {
    firstLine = false;
    yield `if (${spanName}.Length is < ${trie.minLength} or > ${trie.maxLength})`;
    yield `{`;
    yield ind(`goto end;`);
    yield `}`;
  }

  for (const child of trie) {
    if (firstLine) firstLine = false;
    else yield ``;

    if (child.size === 0) {
      // leaf node
      yield `if (${spanName}.SequenceEqual("${child.prefix}"${utf8 ? "u8" : ""}))`;
      yield `{`;
      yield* indent(onMatch(child.value!));
      yield `}`;
    } else {
      const sliceName = `${spanName}_${index++}`;
      yield `if (${spanName}.StartsWith("${child.prefix}"${utf8 ? "u8" : ""}))`;
      yield `{`;

      yield ind(
        `var ${sliceName} = ${spanName}.Slice(${child.prefix.length});`,
      );
      yield* indent(trieMatch(child, sliceName, onMatch, utf8, level + 1));

      yield `}`;
    }
  }

  const value = trie.value;
  if (typeof value !== "undefined") {
    if (firstLine) firstLine = false;
    else yield ``;

    yield `if (${spanName}.Length == 0)`;
    yield `{`;
    yield* indent(onMatch(value));
    yield `}`;
  }

  if (level > 0) {
    yield ``;
    yield `goto end;`;
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
    }),
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
      true,
    ),
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
        }.${match.codeIdentifiers.task};`;
        yield `return true;`;
      }),
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
          yield `role = GuardianshipRoles.${area.identifier}.${match.codeIdentifiers.task};`;
          yield `return true;`;
        },
        true,
      ),
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
  `#pragma warning disable CS0164 // Unreferenced label`,
  ...indent(tryFind()),
  `#pragma warning restore CS0164 // Unreferenced label`,
  `}`,
];

console.log(lines.join("\n"));
