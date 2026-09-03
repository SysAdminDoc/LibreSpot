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

export const ENGINE_VERSION = "4.2.0";

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

  if (!merged.schemes[merged.scheme]) {
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
  return `(function LibreSpotExportedTheme() {
  const root = document.documentElement;
  root.classList.add(...${serializedClasses});
  root.style.setProperty("--librespot-radius", "${state.appearance.radius}px");
  root.style.setProperty("--librespot-font", ${JSON.stringify(state.appearance.fontFamily)});
  root.style.setProperty("--librespot-scale-navigation", "${state.appearance.scale.navigation}");
  root.style.setProperty("--librespot-scale-content", "${state.appearance.scale.content}");
  root.style.setProperty("--librespot-scale-playbar", "${state.appearance.scale.playbar}");
  root.style.setProperty("--librespot-scale-right-sidebar", "${state.appearance.scale.rightSidebar}");
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
