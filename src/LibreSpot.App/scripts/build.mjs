import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, rmSync, writeFileSync } from "node:fs";
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
const brandIcon = resolve(workspace, "src", "icons", "librespot.svg");
const generatedBrandIcon = resolve(
  workspace,
  "src",
  "icons",
  "librespot.generated.txt",
);

rmSync(output, { recursive: true, force: true });
writeFileSync(generatedBrandIcon, readFileSync(brandIcon, "utf8"), "utf8");
try {
  execFileSync(
    process.execPath,
    [creator, "--out", output, "--in", resolve(workspace, "src"), "--minify"],
    {
      cwd: workspace,
      stdio: "inherit",
    },
  );
} finally {
  rmSync(generatedBrandIcon, { force: true });
}

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
const packageMetadata = JSON.parse(
  readFileSync(resolve(workspace, "package.json"), "utf8"),
);
const manifestPath = resolve(output, "manifest.json");
const manifest = JSON.parse(
  readFileSync(manifestPath, "utf8"),
);
manifest.version = packageMetadata.version;
writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
if (
  manifest.name !== "LibreSpot" ||
  manifest.version !== packageMetadata.version ||
  !manifest.subfiles_extension?.includes("librespot-engine.js")
) {
  throw new Error("Custom-app manifest metadata is incomplete.");
}
const index = readFileSync(resolve(output, "index.js"), "utf8");
if (!/(?:const|let|var)\s+render=/.test(index)) {
  throw new Error("Custom-app bundle does not expose Spicetify's render contract.");
}
