import {
  CUSTOMIZATION_CATALOG,
  type CatalogFeature,
  type CatalogPreset,
  type CatalogSnippet,
  type CatalogSpotxSwitch,
  type ColorScheme,
  type EngineState,
} from "../core/index.ts";

export const BUILTIN_SCHEMES: Record<string, ColorScheme> = {
  Dark: {
    text: "FFFFFF",
    subtext: "B3B3B3",
    main: "0E0E10",
    "main-elevated": "18181B",
    highlight: "1E1E22",
    "highlight-elevated": "27272B",
    sidebar: "0A0A0C",
    player: "121214",
    card: "18181B",
    shadow: "000000",
    "selected-row": "FFFFFF",
    button: "1DB954",
    "button-active": "1ED760",
    "button-disabled": "4D4D52",
    "tab-active": "27272B",
    notification: "4687D6",
    "notification-error": "E22134",
    misc: "7F7F7F",
    accent: "1DB954",
  },
  Light: {
    text: "111214",
    subtext: "55585F",
    main: "FFFFFF",
    "main-elevated": "F1F2F5",
    highlight: "ECEDF1",
    "highlight-elevated": "E4E6EB",
    sidebar: "FFFFFF",
    player: "F1F2F5",
    card: "FFFFFF",
    shadow: "C9CCD4",
    "selected-row": "111214",
    button: "188741",
    "button-active": "188741",
    "button-disabled": "C2C6CF",
    "tab-active": "E4E6EB",
    notification: "2E77D0",
    "notification-error": "CD1A2B",
    misc: "8A8D95",
    accent: "188741",
  },
  OLED: {
    text: "FFFFFF",
    subtext: "A0A0A6",
    main: "000000",
    "main-elevated": "0A0A0A",
    highlight: "111111",
    "highlight-elevated": "1A1A1A",
    sidebar: "000000",
    player: "000000",
    card: "0A0A0A",
    shadow: "000000",
    "selected-row": "FFFFFF",
    button: "1ED760",
    "button-active": "1FDF64",
    "button-disabled": "3F3F3F",
    "tab-active": "1A1A1A",
    notification: "4687D6",
    "notification-error": "E22134",
    misc: "6E6E72",
    accent: "1ED760",
  },
  HighContrast: {
    text: "FFFFFF",
    subtext: "FFFFFF",
    main: "000000",
    "main-elevated": "000000",
    highlight: "1A1A1A",
    "highlight-elevated": "2A2A2A",
    sidebar: "000000",
    player: "000000",
    card: "000000",
    shadow: "000000",
    "selected-row": "FFFF00",
    button: "FFFF00",
    "button-active": "FFFFFF",
    "button-disabled": "767676",
    "tab-active": "3A3A3A",
    notification: "66B2FF",
    "notification-error": "FF5C6C",
    misc: "C8C8C8",
    accent: "FFFF00",
  },
};

export type PresetDefinition = CatalogPreset & {
  apply(draft: EngineState): void;
};

function applyPreset(preset: CatalogPreset, draft: EngineState): void {
  const profile = preset.profile;
  draft.name = preset.title;
  draft.theme = profile.theme;
  draft.scheme = profile.scheme;
  draft.effectsTier = profile.effectsTier;
  draft.dynamicAccent.mode = profile.accentMode;
  draft.dynamicAccent.materialPalette = profile.materialPalette;
  draft.enabledSnippets = [...profile.snippets];
  if (profile.contentScale !== undefined) draft.appearance.scale.content = profile.contentScale;
  if (profile.navigationScale !== undefined) draft.appearance.scale.navigation = profile.navigationScale;
  if (profile.playbarScale !== undefined) draft.appearance.scale.playbar = profile.playbarScale;
  if (profile.fontFamily) draft.appearance.fontFamily = profile.fontFamily;
  draft.layers.accessibility = preset.id === "accessibility" || draft.layers.accessibility;
  draft.autoEffects = preset.id !== "performance";
}

export const SURFACE_SNIPPETS: readonly CatalogSnippet[] =
  CUSTOMIZATION_CATALOG.snippets;

export const SURFACE_PRESETS: readonly PresetDefinition[] =
  CUSTOMIZATION_CATALOG.presets.map((preset) => ({
    ...preset,
    apply: (draft) => {
      applyPreset(preset, draft);
    },
  }));

export const SURFACE_SNIPPET_CSS = Object.fromEntries(
  SURFACE_SNIPPETS.map((snippet) => [snippet.id, snippet.css]),
);

export const SURFACE_FEATURE_SEEDS: readonly CatalogFeature[] =
  CUSTOMIZATION_CATALOG.spotifyFeatures;

export const SURFACE_SPOTX_SEEDS: readonly CatalogSpotxSwitch[] =
  CUSTOMIZATION_CATALOG.spotxSwitches;

export const SURFACE_THEMES = [
  ...CUSTOMIZATION_CATALOG.builtInThemes,
  ...CUSTOMIZATION_CATALOG.themes,
] as const;
