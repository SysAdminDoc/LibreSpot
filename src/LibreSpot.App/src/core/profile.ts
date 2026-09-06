import { parseColorIni, serializeColorIni, type ColorIniDocument } from "./color-ini.ts";
import { deriveScheme, normalizeHex } from "./colors.ts";
import { LAYER_CSS } from "./layer-styles.ts";
import { CATALOG_THEME_STYLES } from "./catalog.ts";
import {
  PROFILE_SCHEMA_VERSION,
  createDefaultState,
  type EngineState,
} from "./state.ts";

export type ThemeExport = {
  "color.ini": string;
  "user.css": string;
  "theme.js": string;
};

export const ENGINE_VERSION = "4.5.0";
export const MAX_PROFILE_BYTES = 2 * 1024 * 1024;

const EFFECTS_TIERS = ["glass", "eco", "flat"] as const;
const ACCENT_MODES = ["scheme", "album-art", "fixed", "os"] as const;
const ACCENT_PRESETS = ["VIBRANT", "LIGHT_VIBRANT", "PROMINENT"] as const;
const MATERIAL_VARIANTS = [
  "tonalSpot",
  "fidelity",
  "vibrant",
  "expressive",
  "neutral",
  "monochrome",
  "content",
] as const;

function invalid(path: string, message: string): never {
  throw new Error(`Invalid LibreSpot profile ${path}: ${message}.`);
}

function stringValue(value: unknown, path: string, maxLength = 256): string {
  if (typeof value !== "string" || value.length > maxLength) {
    invalid(path, "expected a bounded string");
  }
  return value;
}

function nonEmptyString(value: unknown, path: string, maxLength = 256): string {
  const result = stringValue(value, path, maxLength);
  if (result.trim().length === 0) invalid(path, "must not be empty");
  return result;
}

function finiteNumber(value: unknown, path: string, minimum?: number, maximum?: number): number {
  if (typeof value !== "number" || !Number.isFinite(value)) {
    invalid(path, "expected a finite number");
  }
  if (minimum !== undefined && value < minimum) invalid(path, `must be at least ${minimum}`);
  if (maximum !== undefined && value > maximum) invalid(path, `must be at most ${maximum}`);
  return value;
}

function booleanValue(value: unknown, path: string): boolean {
  if (typeof value !== "boolean") invalid(path, "expected a boolean");
  return value;
}

function primitiveMap(value: unknown, path: string): Record<string, boolean | number | string> {
  if (!isRecord(value)) invalid(path, "expected an object");
  const result: Record<string, boolean | number | string> = Object.create(null) as Record<string, boolean | number | string>;
  if (Object.keys(value).length > 512) invalid(path, "contains too many entries");
  for (const [key, entry] of Object.entries(value)) {
    nonEmptyString(key, `${path} key`, 128);
    if (typeof entry === "number") finiteNumber(entry, `${path}.${key}`);
    else if (typeof entry !== "boolean" && typeof entry !== "string") {
      invalid(`${path}.${key}`, "expected a string, number, or boolean");
    } else if (typeof entry === "string" && entry.length > 4096) {
      invalid(`${path}.${key}`, "string is too long");
    }
    Object.defineProperty(result, key, {
      value: entry,
      enumerable: true,
      writable: true,
      configurable: true,
    });
  }
  return result;
}

function stringArray(value: unknown, path: string, maxItems = 512): string[] {
  if (!Array.isArray(value) || value.length > maxItems) invalid(path, "expected a bounded string array");
  return value.map((entry, index) => nonEmptyString(entry, `${path}[${index}]`, 256));
}

function colorScheme(value: unknown, path: string): Record<string, string> {
  if (!isRecord(value)) invalid(path, "expected an object");
  if (Object.keys(value).length > 256) invalid(path, "contains too many colors");
  const result: Record<string, string> = Object.create(null) as Record<string, string>;
  for (const [key, entry] of Object.entries(value)) {
    const name = nonEmptyString(key, `${path} key`, 128);
    const color = stringValue(entry, `${path}.${name}`, 32);
    try {
      result[name] = normalizeHex(color);
    } catch {
      invalid(`${path}.${name}`, "expected a 3 or 6 digit hex color");
    }
  }
  return result;
}

function clockValue(value: unknown, path: string): string {
  const clock = stringValue(value, path, 5);
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(clock)) {
    invalid(path, "expected a 24-hour HH:mm value");
  }
  return clock;
}

function validateLayers(value: unknown, path: string): void {
  if (!isRecord(value)) invalid(path, "expected an object");
  for (const key of ["palette", "layout", "effects", "accessibility"] as const) {
    if (key in value) booleanValue(value[key], `${path}.${key}`);
  }
}

function validateSchedule(value: unknown, path: string, schemes: Record<string, unknown>): void {
  if (!isRecord(value)) invalid(path, "expected an object");
  if ("enabled" in value) booleanValue(value.enabled, `${path}.enabled`);
  if ("lightStart" in value) clockValue(value.lightStart, `${path}.lightStart`);
  if ("darkStart" in value) clockValue(value.darkStart, `${path}.darkStart`);
  for (const key of ["lightScheme", "darkScheme"] as const) {
    if (key in value) {
      const scheme = nonEmptyString(value[key], `${path}.${key}`, 128);
      if (!Object.prototype.hasOwnProperty.call(schemes, scheme)) invalid(`${path}.${key}`, `references missing scheme "${scheme}"`);
    }
  }
}

function validateState(value: Record<string, unknown>, path = "state"): void {
  nonEmptyString(value.name, `${path}.name`);
  nonEmptyString(value.theme, `${path}.theme`);
  const selectedScheme = nonEmptyString(value.scheme, `${path}.scheme`);
  if (!isRecord(value.schemes)) invalid(`${path}.schemes`, "expected an object");
  if (Object.keys(value.schemes).length === 0) invalid(`${path}.schemes`, "must contain at least one scheme");
  for (const [name, scheme] of Object.entries(value.schemes)) {
    nonEmptyString(name, `${path}.schemes key`, 128);
    colorScheme(scheme, `${path}.schemes.${name}`);
  }
  if (!Object.prototype.hasOwnProperty.call(value.schemes, selectedScheme)) invalid(`${path}.scheme`, `references missing scheme "${selectedScheme}"`);

  if ("layers" in value) validateLayers(value.layers, `${path}.layers`);
  if ("effectsTier" in value && !EFFECTS_TIERS.includes(value.effectsTier as (typeof EFFECTS_TIERS)[number])) {
    invalid(`${path}.effectsTier`, "contains an unsupported value");
  }
  if ("autoEffects" in value) booleanValue(value.autoEffects, `${path}.autoEffects`);
  if ("lastMeasuredFps" in value && value.lastMeasuredFps !== null) {
    finiteNumber(value.lastMeasuredFps, `${path}.lastMeasuredFps`, 0);
  }

  if ("dynamicAccent" in value) {
    if (!isRecord(value.dynamicAccent)) invalid(`${path}.dynamicAccent`, "expected an object");
    const accent = value.dynamicAccent;
    if ("mode" in accent && !ACCENT_MODES.includes(accent.mode as (typeof ACCENT_MODES)[number])) invalid(`${path}.dynamicAccent.mode`, "contains an unsupported value");
    if ("preset" in accent && !ACCENT_PRESETS.includes(accent.preset as (typeof ACCENT_PRESETS)[number])) invalid(`${path}.dynamicAccent.preset`, "contains an unsupported value");
    if ("fixed" in accent) {
      const fixed = stringValue(accent.fixed, `${path}.dynamicAccent.fixed`, 32);
      try {
        normalizeHex(fixed);
      } catch {
        invalid(`${path}.dynamicAccent.fixed`, "expected a 3 or 6 digit hex color");
      }
    }
    if ("materialPalette" in accent) booleanValue(accent.materialPalette, `${path}.dynamicAccent.materialPalette`);
    if ("materialVariant" in accent && !MATERIAL_VARIANTS.includes(accent.materialVariant as (typeof MATERIAL_VARIANTS)[number])) invalid(`${path}.dynamicAccent.materialVariant`, "contains an unsupported value");
  }

  if ("appearance" in value) {
    if (!isRecord(value.appearance)) invalid(`${path}.appearance`, "expected an object");
    const appearance = value.appearance;
    if ("fontFamily" in appearance) stringValue(appearance.fontFamily, `${path}.appearance.fontFamily`, 512);
    if ("radius" in appearance) finiteNumber(appearance.radius, `${path}.appearance.radius`, 0, 128);
    if ("scale" in appearance) {
      if (!isRecord(appearance.scale)) invalid(`${path}.appearance.scale`, "expected an object");
      for (const key of ["navigation", "content", "playbar", "rightSidebar"] as const) {
        if (key in appearance.scale) finiteNumber(appearance.scale[key], `${path}.appearance.scale.${key}`, 0.1, 4);
      }
    }
  }

  if ("schedule" in value) validateSchedule(value.schedule, `${path}.schedule`, value.schemes);
  for (const key of ["enabledSnippets", "homeSections", "sidebarItems"] as const) {
    if (key in value) stringArray(value[key], `${path}.${key}`);
  }
  for (const key of ["featureOverrides", "spotxSwitches", "spicetifyOptions"] as const) {
    if (key in value) primitiveMap(value[key], `${path}.${key}`);
  }
  if ("userPresets" in value) {
    if (!Array.isArray(value.userPresets) || value.userPresets.length > 128) invalid(`${path}.userPresets`, "expected a bounded array");
    for (const [index, preset] of value.userPresets.entries()) {
      if (!isRecord(preset)) invalid(`${path}.userPresets[${index}]`, "expected an object");
      validateState(preset, `${path}.userPresets[${index}]`);
      nonEmptyString(preset.id, `${path}.userPresets[${index}].id`, 128);
    }
  }
  if ("updatedAt" in value) {
    const updatedAt = stringValue(value.updatedAt, `${path}.updatedAt`, 64);
    if (Number.isNaN(Date.parse(updatedAt))) invalid(`${path}.updatedAt`, "expected an ISO date");
  }
}

export function validateEngineState(state: EngineState): void {
  validateState(state);
}

export function serializeEngineState(state: EngineState): string {
  return `${JSON.stringify(state, null, 2)}\n`;
}

export function serializeProfile(state: EngineState): string {
  const profile = {
    schemaVersion: 1,
    generator: "LibreSpot-Spotify",
    generatorVersion: ENGINE_VERSION,
    createdAt: state.updatedAt,
    profileName: state.name,
    notes: "Exported from the LibreSpot panel in Spotify.",
    settings: {
      Mode: "Custom",
      Spicetify_CustomApps: ["librespot"],
      LibreSpot_EngineProfileJson: JSON.stringify(state),
      LibreSpot_EnabledSnippets: state.enabledSnippets,
      LibreSpot_FeatureOverridesJson: JSON.stringify(state.featureOverrides),
    },
  };
  return `${JSON.stringify(profile, null, 2)}\n`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function parseProfile(source: string): EngineState {
  if (new TextEncoder().encode(source).length > MAX_PROFILE_BYTES) {
    throw new Error(`LibreSpot profile exceeds the ${MAX_PROFILE_BYTES}-byte limit.`);
  }
  const parsed: unknown = JSON.parse(source);
  let value: unknown = parsed;
  if (
    isRecord(parsed) &&
    isRecord(parsed.settings) &&
    typeof parsed.settings.LibreSpot_EngineProfileJson === "string"
  ) {
    value = JSON.parse(parsed.settings.LibreSpot_EngineProfileJson);
  }
  if (!isRecord(value)) {
    throw new Error("LibreSpot profile must be a JSON object.");
  }
  if (value.schemaVersion !== PROFILE_SCHEMA_VERSION) {
    throw new Error(
      `Unsupported LibreSpot profile schema "${String(value.schemaVersion)}".`,
    );
  }
  if (
    typeof value.name !== "string" ||
    typeof value.theme !== "string" ||
    typeof value.scheme !== "string" ||
    !isRecord(value.schemes)
  ) {
    throw new Error("LibreSpot profile is missing its theme identity or schemes.");
  }

  validateState(value);

  const defaults = createDefaultState();
  const merged = {
    ...defaults,
    ...value,
    layers: { ...defaults.layers, ...(isRecord(value.layers) ? value.layers : {}) },
    dynamicAccent: {
      ...defaults.dynamicAccent,
      ...(isRecord(value.dynamicAccent) ? value.dynamicAccent : {}),
    },
    appearance: {
      ...defaults.appearance,
      ...(isRecord(value.appearance) ? value.appearance : {}),
      scale: {
        ...defaults.appearance.scale,
        ...(isRecord(value.appearance) && isRecord(value.appearance.scale)
          ? value.appearance.scale
          : {}),
      },
    },
    schedule: {
      ...defaults.schedule,
      ...(isRecord(value.schedule) ? value.schedule : {}),
    },
  } as EngineState;

  if (!Object.prototype.hasOwnProperty.call(merged.schemes, merged.scheme)) {
    throw new Error(`Profile scheme "${merged.scheme}" is not present.`);
  }
  merged.dynamicAccent.fixed = normalizeHex(merged.dynamicAccent.fixed);
  return merged;
}

function exportThemeRuntime(state: EngineState): string {
  const classes = [
    ...Object.entries(state.layers)
      .filter(([, enabled]) => enabled)
      .map(([name]) => `librespot-layer-${name}`),
    `librespot-tier-${state.effectsTier}`,
  ];
  const serializedClasses = JSON.stringify(classes);
  const serializedRadius = JSON.stringify(`${state.appearance.radius}px`);
  const serializedFont = JSON.stringify(state.appearance.fontFamily);
  const serializedScales = Object.fromEntries(
    Object.entries(state.appearance.scale).map(([key, value]) => [key, JSON.stringify(`${value}`)]),
  ) as Record<string, string>;
  return `(function LibreSpotExportedTheme() {
  const root = document.documentElement;
  root.classList.add(...${serializedClasses});
  root.style.setProperty("--librespot-radius", ${serializedRadius});
  root.style.setProperty("--librespot-font", ${serializedFont});
  root.style.setProperty("--librespot-scale-navigation", ${serializedScales.navigation});
  root.style.setProperty("--librespot-scale-content", ${serializedScales.content});
  root.style.setProperty("--librespot-scale-playbar", ${serializedScales.playbar});
  root.style.setProperty("--librespot-scale-right-sidebar", ${serializedScales.rightSidebar});
})();\n`;
}

export function exportTheme(state: EngineState): ThemeExport {
  const document: ColorIniDocument = {
    sectionOrder: Object.keys(state.schemes),
    schemes: state.schemes,
  };
  const selectedScheme = state.schemes[state.scheme];
  if (!selectedScheme) {
    throw new Error(`Profile scheme "${state.scheme}" is not present.`);
  }
  const complete = deriveScheme(selectedScheme);
  const variables = Object.entries(complete)
    .map(([key, value]) => `  --spice-${key}: #${value};`)
    .join("\n");
  const themeCss = CATALOG_THEME_STYLES[state.theme]?.css ?? "";
  return {
    "color.ini": serializeColorIni(document, { deriveMissing: true }),
    "user.css": `:root {\n${variables}\n}\n\n${LAYER_CSS.trim()}\n\n${themeCss.trim()}\n`,
    "theme.js": exportThemeRuntime(state),
  };
}

export function importColorIniIntoState(
  source: string,
  current = createDefaultState(),
): EngineState {
  const colorDocument = parseColorIni(source);
  const scheme = colorDocument.sectionOrder[0];
  if (!scheme) {
    throw new Error("Imported color.ini did not contain a scheme.");
  }
  return {
    ...current,
    scheme,
    schemes: colorDocument.schemes,
  };
}
