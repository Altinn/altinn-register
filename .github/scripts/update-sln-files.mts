import { Chalk, type ChalkInstance } from "chalk";
import { globby } from "globby";
import { $, within, echo, cd, usePwsh } from "zx";
import path from "node:path";
import fs from "node:fs";

if (process.platform === "win32") {
  usePwsh();
}

const c = new Chalk({ level: 3 });

const p = (p: string, fmt: ChalkInstance = c.yellow) =>
  fmt(path.relative(process.cwd(), p).replaceAll("\\", "/"));

const allSlnFile = path.resolve("Altinn.Register.sln").replaceAll("\\", "/");
const verticalDirs = await globby("src/*", { onlyDirectories: true });

const slnFiles = [
  allSlnFile,
  ...verticalDirs.map((dir) => path.resolve(dir, `${path.basename(dir)}.sln`).replaceAll("\\", "/")),
];

for (const file of slnFiles) {
  await within(async () => {
    const searchRoot = path.dirname(file);
    $.cwd = searchRoot;

    const rootSln = file === allSlnFile;
    echo("");
    echo(`#############################################`);
    echo(`# ${p(file)}`)

    const stat = fs.statSync(file, { throwIfNoEntry: false });
    if (stat == null || !stat.isFile()) {
      echo(`${c.magenta('!')} ${p(file)}`);
      await $`dotnet new sln -n "${path.basename(file, ".sln")}"`;
    }

    var allProjects = (await globby(`**/*.*proj`, { cwd: searchRoot })).map(
      (p) => path.resolve(searchRoot, p).replaceAll("\\", "/")
    );
    var existingProjects = new Set((await $`dotnet sln ${file} list`.text()).split("\n").map((p) => p.trim().replaceAll("\\", "/")).filter(p => p.length > 0 && p.endsWith("proj")));

    let added = 0;
    for (const project of allProjects) {
      const relPath = path.relative(searchRoot, project).replaceAll("\\", "/");
      if (existingProjects.has(relPath)) {
        continue;
      }

      let dirPath = path.dirname(path.dirname(relPath)).replaceAll("\\", "/");
      if (rootSln && dirPath.startsWith("src/")) {
        dirPath = dirPath.substring(4);
      }

      echo(`${c.green('+')} ${p(project)}`);
      await $`dotnet sln ${file} add ${project} --solution-folder ${dirPath}`;
      added++;
    }

    if (added == 0) {
      echo(`${c.green('✓')} No new projects to add`);
    }
  });
}
