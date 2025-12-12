import YAML from "yaml";
import fs from "node:fs/promises";
import { type, type ArkErrors } from "arktype";

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

const GuardianshipDefinition = type({
  area: "string > 0",
  task: "string > 0",
  identifier: type(/^[a-z][a-z0-9]+(?:-[a-z0-9]+)*$/).and("string <= 64"),
  title: LocalizedString,
  description: LocalizedString,
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
  import.meta.url
);

export const getGuardianships = async () => {
  const rawText = await fs.readFile(GUARDIANSHIPS_FILE, "utf-8");
  return YamlSchema.assert(rawText);
};
