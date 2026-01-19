import YAML from "yaml";
import fs from "node:fs/promises";
import { type, type ArkErrors } from "arktype";
import { Trie } from "./trie.mts";

const LocalizedString = type({
  en: "string > 0",
  nb: "string > 0",
  nn: "string > 0",
  "[string]": "string > 0",
});

export type LocalizedString = typeof LocalizedString.infer;

const HasIdentifier = type({
  identifier: "string > 0",
});

const NprMapping = type({
  virksomhet: type(/^[a-z]+([A-Z][a-z]+)*$/),
  oppgave: type(/^[a-z]+([A-Z][a-z]+)*$/),
});

const Mappings = type({
  npr: NprMapping,
});

const GuardianshipDefinition = type({
  area: "string > 0",
  task: "string > 0",
  identifier: type(/^[a-z][a-z0-9]+(?:-[a-z0-9]+)*$/).and("string <= 64"),
  title: LocalizedString,
  description: LocalizedString,
  mapping: Mappings,
}).pipe((obj) => {
  const areaIdentifier = toIdentifier(obj.area);
  const taskIdentifier = toIdentifier(obj.task);

  return {
    ...obj,
    codeIdentifiers: {
      area: areaIdentifier,
      task: taskIdentifier,
    },
  };
});

export type GuardianshipDefinition = typeof GuardianshipDefinition.infer;

const YamlSchema = type("string").pipe((text, ctx) => {
  const lineCounter = new YAML.LineCounter();
  const identifiers = new Map<
    string,
    {
      pos: { line: number };
      result: ArkErrors | GuardianshipDefinition;
    }[]
  >();
  for (const doc of YAML.parseAllDocuments(text, { lineCounter })) {
    if (doc.errors.length > 0) {
      for (const err of doc.errors) {
        const pos = err.linePos![0];
        ctx.error({
          problem: "Invalid YAML syntax",
          message: err.message,
          propString: `Line ${pos.line}, Column ${pos.col}`,
        });
      }

      continue;
    }

    const linePos = lineCounter.linePos(doc.range[0]);
    const obj = doc.toJS();
    ctx.path.push(`Document starting at line ${linePos.line}`);
    const identifierResult = HasIdentifier(obj);
    if (identifierResult instanceof type.errors) {
      ctx.errors.merge(identifierResult);
    }
    ctx.path.pop();

    if (identifierResult instanceof type.errors) {
      continue;
    }

    let idEntries = identifiers.get(identifierResult.identifier);
    if (!idEntries) {
      idEntries = [];
      identifiers.set(identifierResult.identifier, idEntries);
    }
    idEntries.push({
      pos: linePos,
      result: GuardianshipDefinition(obj),
    });
  }

  const results: (typeof GuardianshipDefinition.infer)[] = [];
  for (const [identifier, entries] of identifiers) {
    ctx.path.push(identifier);
    if (entries.length > 1) {
      ctx.error({
        problem: `Duplicate guardianship identifier: ${identifier}`,
        message: `The identifier '${identifier}' is used in multiple documents.`,
      });
    }

    for (const entry of entries) {
      if (entry.result instanceof type.errors) {
        ctx.errors.merge(entry.result);
      } else {
        results.push(entry.result);
      }
    }
    ctx.path.pop();
  }

  if (ctx.hasError()) {
    return ctx.errors[0]!;
  }

  return results;
});

const GUARDIANSHIPS_FILE = new URL(
  "../../../data/guardianships.yaml",
  import.meta.url,
);

function toIdentifier(name: string): string {
  name = name.toLowerCase().replace(/[-_ \/]+(.)/g, (_, c) => c.toUpperCase());
  return name.charAt(0).toUpperCase() + name.slice(1);
}

export const getGuardianships = async () => {
  const rawText = await fs.readFile(GUARDIANSHIPS_FILE, "utf-8");
  return YamlSchema.assert(rawText);
};

export type GuardianshipArea = {
  readonly name: string;
  readonly identifier: string;
  readonly tasks: Map<string, GuardianshipTask>;
  readonly trie: Trie<GuardianshipDefinition>;

  readonly mapping: {
    readonly npr: string;
  };
};

export type GuardianshipTask = {
  readonly name: string;
  readonly guardianship: GuardianshipDefinition;
  readonly identifier: string;

  readonly mapping: {
    readonly npr: string;
  };
};

export type GuardianshipAreas = {
  readonly map: Map<string, GuardianshipArea>;
  readonly trie: Trie<GuardianshipArea>;
};

export const getAreas = async (): Promise<GuardianshipAreas> => {
  type MutableGuardianshipArea = {
    readonly name: string;
    readonly identifier: string;
    tasks: Map<string, GuardianshipTask>;
    trie: Trie<GuardianshipDefinition>;

    readonly mapping: {
      readonly npr: string;
    };
  };

  const areas: Map<string, MutableGuardianshipArea> = new Map();

  for (const guardianship of await getGuardianships()) {
    let area = areas.get(guardianship.area);
    if (!area) {
      area = {
        name: guardianship.area,
        identifier: guardianship.codeIdentifiers.area,
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
        `Duplicate guardianship for area="${guardianship.area}", task="${guardianship.task}"`,
      );
    }

    if (area.mapping.npr !== guardianship.mapping.npr.virksomhet) {
      throw new Error(
        `Inconsistent npr mapping for area="${guardianship.area}": ` +
          `"${area.mapping.npr}" vs "${guardianship.mapping.npr.virksomhet}"`,
      );
    }

    area.tasks.set(guardianship.task, {
      name: guardianship.task,
      identifier: guardianship.codeIdentifiers.task,
      guardianship,

      mapping: {
        npr: guardianship.mapping.npr.oppgave,
      },
    });
  }

  const trie = Trie.from(
    [...areas.values()].map((area) => {
      area.trie = Trie.from(
        [...area.tasks.values()].map((g) => [g.mapping.npr, g.guardianship]),
      );
      return [area.mapping.npr, area as GuardianshipArea];
    }),
  );

  return {
    map: areas as Map<string, GuardianshipArea>,
    trie,
  };
};
