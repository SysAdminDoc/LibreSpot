import { createHash } from "node:crypto";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "../../..");
const catalogPath = resolve(repositoryRoot, "schemas/librespot-customization.json");
const themeManifestPath = resolve(repositoryRoot, "schemas/theme-preview-manifest.json");
const communityAssetsPath = resolve(repositoryRoot, "schemas/community-assets.json");
const parityManifestPath = resolve(repositoryRoot, "schemas/parity-manifest.json");
const defaultSpotxPath = resolve(
  repositoryRoot,
  "work/librespot-engine-upstream/spotx/patches/patches.json",
);

const metadataOnlySpotxKeys = new Set([
  "SpotX_CustomPatchesSourceUrl",
  "SpotX_CustomPatchesFetchedAtUtc",
  "SpotX_CustomPatchesSourceByteCount",
  "SpotX_CustomPatchesSourceSha256",
]);

const requiredSpicetifyOptions = new Set([
  "spotify_path",
  "prefs_path",
  "current_theme",
  "color_scheme",
  "inject_theme_js",
  "inject_css",
  "replace_colors",
  "overwrite_assets",
  "spotify_launch_flags",
  "check_spicetify_update",
  "always_enable_devtools",
  "disable_sentry",
  "disable_ui_logging",
  "remove_rtl_rule",
  "expose_apis",
  "extensions",
  "custom_apps",
  "sidebar_config",
  "home_config",
  "experimental_features",
  "Patch",
]);

function decodeString(token) {
  return JSON.parse(token);
}

function readObject(source, start) {
  let depth = 0;
  let quote = "";
  let escaped = false;
  for (let index = start; index < source.length; index += 1) {
    const character = source[index];
    if (quote) {
      if (escaped) escaped = false;
      else if (character === "\\") escaped = true;
      else if (character === quote) quote = "";
      continue;
    }
    if (character === '"' || character === "'") quote = character;
    else if (character === "{") depth += 1;
    else if (character === "}" && --depth === 0) return source.slice(start, index + 1);
  }
  throw new Error(`Unclosed feature declaration at byte ${start}.`);
}

function readToken(objectSource, key) {
  const escapedKey = key.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return new RegExp(`(?:^|,)${escapedKey}:([^,}]+)`).exec(objectSource)?.[1];
}

function parseNumber(token) {
  if (!token || !/^-?(?:0x[\da-f]+|\d+(?:\.\d+)?(?:e[+-]?\d+)?)$/i.test(token)) {
    return undefined;
  }
  const value = Number(token);
  return Number.isFinite(value) ? value : undefined;
}

function parseDefault(token, hasValues) {
  if (token === "!0") return true;
  if (token === "!1") return false;
  if (token?.startsWith('"')) return decodeString(token);
  const number = parseNumber(token);
  if (number !== undefined) return number;
  if (hasValues && token) return token.split(".").at(-1)?.toLowerCase() ?? token;
  return null;
}

function resolveEnumValues(source, declarationIndex, token, defaultValue) {
  const values = new Set(typeof defaultValue === "string" ? [defaultValue] : []);
  if (!token || !/^[A-Za-z_$][\w$]*$/.test(token)) return [...values];
  const prefix = source.slice(Math.max(0, declarationIndex - 12_000), declarationIndex);
  const escapedToken = token.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const assignments = [...prefix.matchAll(new RegExp(`(?:^|[,;])(?:var |let |const )?${escapedToken}=`, "g"))];
  const assignment = assignments.at(-1);
  if (!assignment) return [...values];
  const candidate = prefix.slice(assignment.index, assignment.index + 3000).split(";")[0] ?? "";
  for (const valueMatch of candidate.matchAll(/\.[A-Za-z_$][\w$]*=("(?:\\.|[^"\\])*")/g)) {
    values.add(decodeString(valueMatch[1]));
  }
  return [...values];
}

export function classifyFeature(name, description) {
  const value = `${name} ${description}`.toLowerCase();
  if (/easter|stranger|mandalor|balloon|anniversar|funny|celebrat/.test(value)) return "Fun";
  if (/lyric|karaoke|sing.?along|transcript/.test(value)) return "Lyrics";
  if (/now.?playing|npv|pip|mini.?player|canvas|fullscreen/.test(value)) return "Now Playing";
  if (/\bhome\b|homepage|home.?shelf|shortcut.?grid/.test(value)) return "Home";
  if (/library|collection|playlist|album|artist|folder|download|saved.?track/.test(value)) return "Library";
  if (/\bad\b|advert|sponsor|telemetry|sentry|tracking|attribution|impression|premium|upsell|upgrade|fraud/.test(value)) return "Ads and tracking";
  if (/playback|player|audio|queue|volume|crossfade|automix|gapless|transition|codec|video/.test(value)) return "Playback";
  if (/layout|navigation|nav.?bar|sidebar|top.?bar|title.?bar|window|desktop|search|theme|ui\b/.test(value)) return "Layout";
  return "Everything else";
}

function isServerGated(name, description) {
  return /premium|free user|subscription|account|server|employee|\bjam\b|enhance|quality tier|remote download/i.test(
    `${name} ${description}`,
  );
}

export function extractSpotifyFeatures(source) {
  const declaration = /\{name:("(?:\\.|[^"\\])*"),description:("(?:\\.|[^"\\])*"),/g;
  const features = [];
  for (const match of source.matchAll(declaration)) {
    const name = decodeString(match[1]);
    if (name.startsWith("#")) continue;
    const description = decodeString(match[2]);
    const objectSource = readObject(source, match.index);
    const valuesToken = readToken(objectSource, "values");
    const defaultToken = readToken(objectSource, "default");
    const defaultValue = parseDefault(defaultToken, Boolean(valuesToken));
    const type = valuesToken
      ? "enum"
      : typeof defaultValue === "boolean"
        ? "bool"
        : typeof defaultValue === "number"
          ? "number"
          : "string";
    const feature = {
      name,
      description,
      type,
      default: defaultValue,
      group: classifyFeature(name, description),
      serverGated: isServerGated(name, description),
      source: "Spotify xpui.js 1.2.93",
    };
    if (type === "enum") {
      feature.values = resolveEnumValues(source, match.index, valuesToken, defaultValue);
    }
    const minimum = parseNumber(readToken(objectSource, "lower"));
    const maximum = parseNumber(readToken(objectSource, "upper"));
    if (minimum !== undefined) feature.minimum = minimum;
    if (maximum !== undefined) feature.maximum = maximum;
    features.push(feature);
  }
  features.sort((left, right) => left.name.localeCompare(right.name));
  if (features.length < 300 || new Set(features.map((feature) => feature.name)).size !== features.length) {
    throw new Error(`Expected more than 300 unique Spotify features, found ${features.length}.`);
  }
  return features;
}

function versionTuple(value) {
  return String(value || "")
    .split(".")
    .slice(0, 3)
    .map((part) => Number.parseInt(part, 10) || 0);
}

function compareVersions(left, right) {
  const a = versionTuple(left);
  const b = versionTuple(right);
  for (let index = 0; index < 3; index += 1) {
    const difference = (a[index] ?? 0) - (b[index] ?? 0);
    if (difference !== 0) return difference;
  }
  return 0;
}

function activeForVersion(entry, version) {
  const from = entry.version?.fr;
  const to = entry.version?.to;
  return (!from || compareVersions(version, from) >= 0) && (!to || compareVersions(version, to) <= 0);
}

export function extractSpotxFeatureOverrides(document, spotifyVersion, declaredNames) {
  const groups = [
    ["DisableExp", "disable", false],
    ["EnableExp", "enable", true],
    ["CustomExp", "custom", undefined],
  ];
  const overrides = new Map();
  for (const [property, mode, fixedValue] of groups) {
    for (const entry of Object.values(document.others?.[property] ?? {})) {
      if (!entry?.name || !activeForVersion(entry, spotifyVersion)) continue;
      overrides.set(entry.name, {
        name: entry.name,
        description: entry.native_description || entry.description || entry.name,
        value: fixedValue ?? entry.value,
        mode,
        source: `SpotX patches.json ${spotifyVersion}`,
        version: entry.version,
        declaredBySpotify: declaredNames.has(entry.name),
      });
    }
  }
  return [...overrides.values()].sort((left, right) => left.name.localeCompare(right.name));
}

function sha256(source) {
  return createHash("sha256").update(source).digest("hex");
}

function json(path) {
  return JSON.parse(readFileSync(path, "utf8"));
}

function locateXpui(explicitPath) {
  const candidates = [
    explicitPath,
    process.env.LIBRESPOT_XPUI_JS,
    process.env.APPDATA && resolve(process.env.APPDATA, "spicetify/Extracted/Raw/xpui/xpui.js"),
    process.env.APPDATA && resolve(process.env.APPDATA, "Spotify/Apps/xpui/xpui.js"),
  ].filter(Boolean);
  const found = candidates.find((candidate) => existsSync(candidate));
  if (!found) throw new Error("Set LIBRESPOT_XPUI_JS to the pinned 1.2.93 xpui.js file.");
  return found;
}

function optionValue(name) {
  const prefix = `--${name}=`;
  return process.argv.find((argument) => argument.startsWith(prefix))?.slice(prefix.length);
}

function uniqueById(items) {
  const result = new Map();
  for (const item of items) result.set(item.id, item);
  return [...result.values()];
}

function populateAssetCatalog(catalog) {
  const themeManifest = json(themeManifestPath);
  const assets = json(communityAssetsPath);
  catalog.themes = themeManifest.themes.map((theme) => ({
    id: theme.id,
    title: theme.id,
    schemes: theme.schemes,
    source: theme.sourceRepo || theme.source,
    commit: theme.commitSha,
    requiresJs: theme.requiresJs,
    marketplaceOnly: theme.marketplaceOnly,
    supportState: theme.supportState,
    lastVerifiedSpotify: catalog.pins.spotifyVersion,
  }));
  const communityExtensions = assets.extensions.map((extension) => ({
    id: extension.filename,
    title: extension.displayName,
    description: extension.description,
    source: `${extension.owner}/${extension.repo}`,
    commit: extension.commitSha,
    sha256: extension.sha256,
    license: extension.spdxLicense,
    supportState: extension.supportState,
    lastVerifiedSpotify: catalog.pins.spotifyVersion,
    liveToggle: false,
  }));
  catalog.extensions = uniqueById([
    ...catalog.extensions,
    {
      id: "librespot-engine.js",
      title: "LibreSpot live engine",
      description: "Owns live state, runtime styles, feature overrides, and named health checks.",
      source: "SysAdminDoc/LibreSpot",
      lastVerifiedSpotify: catalog.pins.spotifyVersion,
      liveToggle: false,
    },
    ...communityExtensions,
  ]);
  const communityApps = assets.customApps.map((app) => ({
    id: app.appId,
    title: app.displayName,
    description: app.description,
    source: `${app.owner}/${app.repo}`,
    commit: app.commitSha,
    version: app.releaseTag,
    sha256: app.sha256,
    license: app.spdxLicense,
    supportState: app.supportState,
    lastVerifiedSpotify: catalog.pins.spotifyVersion,
  }));
  catalog.customApps = uniqueById([...catalog.customApps, ...communityApps]);
}

function refresh() {
  const catalog = json(catalogPath);
  const xpuiPath = locateXpui(optionValue("xpui"));
  const xpuiSource = readFileSync(xpuiPath, "utf8");
  const features = extractSpotifyFeatures(xpuiSource);
  const spotxPath = optionValue("spotx") || defaultSpotxPath;
  if (!existsSync(spotxPath)) throw new Error(`SpotX patch table not found: ${spotxPath}`);
  const overrides = extractSpotxFeatureOverrides(
    json(spotxPath),
    catalog.pins.spotifyVersion,
    new Set(features.map((feature) => feature.name)),
  );
  const overrideMap = new Map(overrides.map((override) => [override.name, override]));
  for (const feature of features) {
    const override = overrideMap.get(feature.name);
    if (!override) continue;
    feature.spotxForced = { value: override.value, mode: override.mode, source: override.source };
    if (feature.type === "enum" && typeof override.value === "string" && !feature.values.includes(override.value)) {
      feature.values.push(override.value);
    }
  }
  catalog.pins.xpuiSha256 = sha256(xpuiSource);
  catalog.spotifyFeatures = features;
  catalog.spotxFeatureOverrides = overrides;
  populateAssetCatalog(catalog);
  writeFileSync(catalogPath, `${JSON.stringify(catalog, null, 2)}\n`, "utf8");
  console.log(`Catalog refreshed from ${xpuiPath}: ${features.length} Spotify flags, ${overrides.length} active SpotX overrides.`);
}

function collectCatalogSpotxKeys(catalog) {
  return new Set(
    catalog.spotxSwitches.flatMap((item) => [item.configKey, ...(item.relatedConfigKeys ?? [])]),
  );
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function truth() {
  const catalog = json(catalogPath);
  const xpuiPath = locateXpui(optionValue("xpui"));
  const xpuiSource = readFileSync(xpuiPath, "utf8");
  const actualFeatures = extractSpotifyFeatures(xpuiSource);
  assert(catalog.pins.xpuiSha256 === sha256(xpuiSource), "The xpui.js SHA256 pin is stale.");
  assert(catalog.spotifyFeatures.length === actualFeatures.length, "The Spotify feature count is stale.");
  const catalogFeatures = new Map(catalog.spotifyFeatures.map((feature) => [feature.name, feature]));
  for (const feature of actualFeatures) {
    const saved = catalogFeatures.get(feature.name);
    assert(saved, `Spotify feature missing from the catalog: ${feature.name}`);
    assert(saved.description === feature.description, `Spotify description changed: ${feature.name}`);
    assert(saved.type === feature.type, `Spotify feature type changed: ${feature.name}`);
    assert(JSON.stringify(saved.default) === JSON.stringify(feature.default), `Spotify feature default changed: ${feature.name}`);
  }
  const parity = json(parityManifestPath);
  const representedSpotxKeys = collectCatalogSpotxKeys(catalog);
  for (const entry of parity.configKeys.filter((item) => item.key.startsWith("SpotX_"))) {
    assert(
      representedSpotxKeys.has(entry.key) || metadataOnlySpotxKeys.has(entry.key),
      `SpotX configuration key is not surfaced: ${entry.key}`,
    );
  }
  const optionIds = new Set(catalog.spicetifyOptions.map((option) => option.id));
  for (const id of requiredSpicetifyOptions) assert(optionIds.has(id), `Spicetify option is missing: ${id}`);
  assert(new Set(catalog.snippets.map((snippet) => snippet.id)).size === catalog.snippets.length, "Snippet IDs must be unique.");
  for (const snippet of catalog.snippets) {
    assert(snippet.lastVerifiedSpotify === catalog.pins.spotifyVersion, `Snippet pin is stale: ${snippet.id}`);
    assert(!snippet.css.includes(":has("), `Snippet uses :has(): ${snippet.id}`);
    assert(!/\.[A-Za-z0-9_]{18,}/.test(snippet.css), `Snippet uses a likely hashed class: ${snippet.id}`);
  }
  assert(catalog.builtInThemes.some((theme) => theme.id === "Prism"), "Prism is missing.");
  assert(catalog.builtInThemes.some((theme) => theme.id === "Compact"), "Compact is missing.");
  assert(catalog.builtInThemes.some((theme) => theme.id === "Accessibility"), "Accessibility is missing.");
  assert(catalog.presets.length >= 4, "The four built-in presets are required.");
  assert(catalog.spotifyFeatures.length > 300, "The pinned bundle should expose more than 300 flags.");
  console.log(`Catalog truth passed: ${catalog.spotifyFeatures.length} Spotify flags, ${catalog.spotxSwitches.length} SpotX controls, ${catalog.snippets.length} snippets, ${catalog.themes.length + catalog.builtInThemes.length} themes.`);
}

const command = process.argv[2];
if (command === "refresh") refresh();
else if (command === "truth") truth();
else if (command) throw new Error(`Unknown catalog command: ${command}`);
