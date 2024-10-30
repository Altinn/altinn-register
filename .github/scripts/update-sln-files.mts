import { Chalk } from "chalk";
import { globby } from "globby";
import { $, within, echo, cd, usePwsh } from "zx";
import path from "node:path";

if (process.platform === "win32") {
  usePwsh();
}

const c = new Chalk({ level: 3 });

const allSlnFile = path.resolve("Altinn.Register.sln");
const slnFiles = await globby("src/*/*.sln", { absolute: true });
slnFiles.unshift(allSlnFile);

for (const file of slnFiles) {
  await within(async () => {
    echo("");
    echo(`#############################################`);
    echo(`Updating ${c.yellow(file)}`);
    const dir = path.dirname(file);
    const fileName = path.basename(file);
    cd(dir);

    const projects = await globby(`**/*.*proj`);
    for (const project of projects) {
      echo(` - Adding ${c.green(project)} to ${c.yellow(file)}`);
      await $`dotnet sln "${fileName}" add "${project}"`;
    }
  });
}
