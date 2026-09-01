import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, rmSync } from "node:fs";
import { resolve } from "node:path";

const workspace = resolve(import.meta.dirname, "..");
const output = resolve(workspace, "dist");
const creator = resolve(
  workspace,
  "node_modules",
  "spicetify-creator",
  "dist",
  "index.js",
);

rmSync(output, { recursive: true, force: true });
execFileSync(
  process.execPath,
  [creator, "--out", output, "--in", resolve(workspace, "src"), "--minify"],
  {
    cwd: workspace,
    stdio: "inherit",
  },
);

const requiredFiles = [
  "index.js",
  "style.css",
  "manifest.json",
  "librespot-engine.js",
];
for (const file of requiredFiles) {
  if (!existsSync(resolve(output, file))) {
    throw new Error(`Custom-app bundle is missing ${file}.`);
  }
}
const manifest = JSON.parse(
  readFileSync(resolve(output, "manifest.json"), "utf8"),
);
if (
  manifest.name !== "LibreSpot" ||
  !manifest.subfiles_extension?.includes("librespot-engine.js")
) {
  throw new Error("Custom-app manifest does not register the companion extension.");
}
const index = readFileSync(resolve(output, "index.js"), "utf8");
if (!/(?:const|let|var)\s+render=/.test(index)) {
  throw new Error("Custom-app bundle does not expose Spicetify's render contract.");
}
