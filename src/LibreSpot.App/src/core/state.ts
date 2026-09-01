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
    homeSections: [],
    sidebarItems: [],
    updatedAt: now.toISOString(),
  };
}

export function cloneState(state: EngineState): EngineState {
  return structuredClone(state);
}
