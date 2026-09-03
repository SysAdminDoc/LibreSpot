import { existsSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import * as esbuild from "esbuild";

// Builds the Spicetify custom app and its companion extension with the same
// esbuild the rest of this workspace pins. It replaces spicetify-creator, which
// has no releases, no repository, and pinned esbuild ^0.14, dragging a second
// six-year-old copy of the bundler into the lockfile.
//
// The output contract Spicetify expects is reproduced exactly: an IIFE bound to
// the app's nameId that exports `default`, followed by a global `render` binding
// that calls it.

const workspace = resolve(import.meta.dirname, "..");
const source = resolve(workspace, "src");
const output = resolve(workspace, "dist");
const brandIcon = resolve(source, "icons", "librespot.svg");
const generatedBrandIcon = resolve(source, "icons", "librespot.generated.txt");

const settings = JSON.parse(readFileSync(resolve(source, "settings.json"), "utf8"));
const packageMetadata = JSON.parse(readFileSync(resolve(workspace, "package.json"), "utf8"));

rmSync(output, { recursive: true, force: true });

// The reviewed brand mark is staged as a text module so it can be imported and
// inlined without committing a duplicate of the SVG.
writeFileSync(generatedBrandIcon, readFileSync(brandIcon, "utf8"), "utf8");
try {
  await esbuild.build({
    entryPoints: [resolve(source, "app.ts")],
    outfile: resolve(output, "index.js"),
    bundle: true,
    minify: true,
    format: "iife",
    globalName: settings.nameId,
    platform: "browser",
    target: "es2020",
    legalComments: "none",
    loader: { ".txt": "text", ".svg": "text" },
    // Spicetify calls this global to mount the app.
    footer: { js: `let render=()=>${settings.nameId}.default();` },
  });

  await esbuild.build({
    entryPoints: [resolve(source, "extensions", "librespot-engine.ts")],
    outfile: resolve(output, "librespot-engine.js"),
    bundle: true,
    minify: true,
    format: "iife",
    platform: "browser",
    target: "es2020",
    legalComments: "none",
    loader: { ".txt": "text", ".svg": "text" },
  });
} finally {
  rmSync(generatedBrandIcon, { force: true });
}

// esbuild names the extracted stylesheet after its entry point.
const bundledCss = resolve(output, "index.css");
if (!existsSync(bundledCss)) {
  throw new Error("Custom-app bundle produced no stylesheet.");
}
renameSync(bundledCss, resolve(output, "style.css"));

const manifest = {
  name: settings.displayName,
  icon: readFileSync(resolve(source, settings.icon), "utf8"),
  "active-icon": readFileSync(resolve(source, settings.activeIcon), "utf8"),
  subfiles: [],
  subfiles_extension: ["librespot-engine.js"],
  version: packageMetadata.version,
};
writeFileSync(
  resolve(output, "manifest.json"),
  `${JSON.stringify(manifest, null, 2)}\n`,
  "utf8",
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

if (
  manifest.name !== "LibreSpot" ||
  manifest.version !== packageMetadata.version ||
  !manifest.subfiles_extension.includes("librespot-engine.js") ||
  !manifest.icon.includes("<svg") ||
  !manifest["active-icon"].includes("<svg")
) {
  throw new Error("Custom-app manifest metadata is incomplete.");
}

const index = readFileSync(resolve(output, "index.js"), "utf8");
if (!/(?:const|let|var)\s+render=/.test(index)) {
  throw new Error("Custom-app bundle does not expose Spicetify's render contract.");
}
if (!index.includes(`${settings.nameId}.default()`)) {
  throw new Error("Custom-app render binding does not call the app's default export.");
}

const extension = readFileSync(resolve(output, "librespot-engine.js"), "utf8");
if (!extension.includes("__libreSpotEngineLoaded")) {
  throw new Error("Companion extension bundle is missing its load marker.");
}
