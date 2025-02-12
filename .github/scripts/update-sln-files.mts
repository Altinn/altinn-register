import { Chalk } from "chalk";
import { globby } from "globby";
import { $, within, echo, cd, usePwsh } from "zx";
import path from "node:path";

if (process.platform === "win32") {
  usePwsh();
}

const c = new Chalk({ level: 3 });

const allSlnFile = path.resolve("Altinn.Register.slnx");
const slnFiles = [
  allSlnFile,
  ...(await globby("src/*/*.sln", { absolute: true })),
  ...(await globby("src/*/*.slnx", { absolute: true })),
];

for (const file of slnFiles) {
  await within(async () => {
    const rootSln = file === allSlnFile;
    echo("");
    echo(`#############################################`);
    echo(`Updating ${c.yellow(file)}`);
    const dir = path.dirname(file);
    const fileName = path.basename(file);
    cd(dir);

    const projects = await globby(`**/*.*proj`);
    for (const project of projects) {
      let dirPath = path.dirname(path.dirname(project));
      if (rootSln && dirPath.startsWith("src/")) {
        dirPath = dirPath.substring(4);
      }

      echo(` - Adding ${c.green(project)} to ${c.yellow(file)}`);
      await $`dotnet sln "${fileName}" add "${project}" --solution-folder "${dirPath}"`;
    }
  });
}
