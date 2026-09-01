import type { ColorScheme } from "./colors.ts";

export const PROFILE_SCHEMA_VERSION = 1 as const;

export type EffectsTier = "glass" | "eco" | "flat";
export type AccentMode = "scheme" | "album-art" | "fixed" | "os";
export type AccentPreset = "VIBRANT" | "LIGHT_VIBRANT" | "PROMINENT";
export type MaterialVariant =
  | "tonalSpot"
  | "fidelity"
  | "vibrant"
  | "expressive"
  | "neutral"
  | "monochrome"
  | "content";

export type LayerState = {
  palette: boolean;
  layout: boolean;
  effects: boolean;
  accessibility: boolean;
};

export type UserPreset = {
  id: string;
  name: string;
  theme: string;
  scheme: string;
  schemes: Record<string, ColorScheme>;
  layers: LayerState;
  effectsTier: EffectsTier;
  autoEffects: boolean;
  dynamicAccent: EngineState["dynamicAccent"];
  appearance: EngineState["appearance"];
  schedule: EngineState["schedule"];
  enabledSnippets: string[];
  featureOverrides: Record<string, boolean | number | string>;
  spotxSwitches: Record<string, boolean | number | string>;
  spicetifyOptions: Record<string, boolean | number | string>;
};

export type EngineState = {
  schemaVersion: typeof PROFILE_SCHEMA_VERSION;
  name: string;
  theme: string;
  scheme: string;
  schemes: Record<string, ColorScheme>;
  layers: LayerState;
  effectsTier: EffectsTier;
  autoEffects: boolean;
  lastMeasuredFps: number | null;
  dynamicAccent: {
    mode: AccentMode;
    preset: AccentPreset;
    fixed: string;
    materialPalette: boolean;
    materialVariant: MaterialVariant;
  };
  appearance: {
    fontFamily: string;
    radius: number;
    scale: {
      navigation: number;
      content: number;
      playbar: number;
      rightSidebar: number;
    };
  };
  schedule: {
    enabled: boolean;
    lightStart: string;
    darkStart: string;
    lightScheme: string;
    darkScheme: string;
  };
  enabledSnippets: string[];
  featureOverrides: Record<string, boolean | number | string>;
  spotxSwitches: Record<string, boolean | number | string>;
  spicetifyOptions: Record<string, boolean | number | string>;
  userPresets: UserPreset[];
  homeSections: string[];
  sidebarItems: string[];
  updatedAt: string;
};

export function createDefaultState(now = new Date(0)): EngineState {
  return {
    schemaVersion: PROFILE_SCHEMA_VERSION,
    name: "Custom",
    theme: "Prism",
    scheme: "Dark",
    schemes: {},
    layers: {
      palette: true,
      layout: true,
      effects: true,
      accessibility: true,
    },
    effectsTier: "glass",
    autoEffects: true,
    lastMeasuredFps: null,
    dynamicAccent: {
      mode: "album-art",
      preset: "VIBRANT",
      fixed: "1ED760",
      materialPalette: false,
      materialVariant: "tonalSpot",
    },
    appearance: {
      fontFamily: "SpotifyMixUI, CircularSp, sans-serif",
      radius: 12,
      scale: {
        navigation: 1,
        content: 1,
        playbar: 1,
        rightSidebar: 1,
      },
    },
    schedule: {
      enabled: false,
      lightStart: "07:00",
      darkStart: "19:00",
      lightScheme: "Light",
      darkScheme: "Dark",
    },
    enabledSnippets: [],
    featureOverrides: {},
    spotxSwitches: {},
    spicetifyOptions: {},
    userPresets: [],
    homeSections: [],
    sidebarItems: [],
    updatedAt: now.toISOString(),
  };
}

export function captureUserPreset(
  state: EngineState,
  id: string,
  name: string,
): UserPreset {
  return {
    id,
    name,
    theme: state.theme,
    scheme: state.scheme,
    schemes: structuredClone(state.schemes),
    layers: structuredClone(state.layers),
    effectsTier: state.effectsTier,
    autoEffects: state.autoEffects,
    dynamicAccent: structuredClone(state.dynamicAccent),
    appearance: structuredClone(state.appearance),
    schedule: structuredClone(state.schedule),
    enabledSnippets: [...state.enabledSnippets],
    featureOverrides: structuredClone(state.featureOverrides),
    spotxSwitches: structuredClone(state.spotxSwitches),
    spicetifyOptions: structuredClone(state.spicetifyOptions),
  };
}

export function applyUserPreset(draft: EngineState, preset: UserPreset): void {
  draft.name = preset.name;
  draft.theme = preset.theme;
  draft.scheme = preset.scheme;
  draft.schemes = structuredClone(preset.schemes);
  draft.layers = structuredClone(preset.layers);
  draft.effectsTier = preset.effectsTier;
  draft.autoEffects = preset.autoEffects;
  draft.dynamicAccent = structuredClone(preset.dynamicAccent);
  draft.appearance = structuredClone(preset.appearance);
  draft.schedule = structuredClone(preset.schedule);
  draft.enabledSnippets = [...preset.enabledSnippets];
  draft.featureOverrides = structuredClone(preset.featureOverrides);
  draft.spotxSwitches = structuredClone(preset.spotxSwitches);
  draft.spicetifyOptions = structuredClone(preset.spicetifyOptions);
}

export function cloneState(state: EngineState): EngineState {
  return structuredClone(state);
}
