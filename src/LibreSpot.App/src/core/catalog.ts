import catalogDocument from "../../../../schemas/librespot-customization.json";
import type { FeatureValue } from "./feature-overrides.ts";

export type CatalogFeature = {
  name: string;
  description: string;
  type: "bool" | "enum" | "number" | "string";
  default: FeatureValue;
  values?: string[];
  minimum?: number;
  maximum?: number;
  group: string;
  serverGated: boolean;
  source: string;
  spotxForced?: {
    value: FeatureValue;
    mode: "enable" | "disable" | "custom";
    source: string;
  };
};

export type CatalogSnippet = {
  id: string;
  title: string;
  description: string;
  category: string;
  css: string;
  preview: string;
  source: string;
  sourceTitle: string;
  lastVerifiedSpotify: string;
  live: boolean;
};

export type CatalogPreset = {
  id: string;
  title: string;
  description: string;
  profile: {
    theme: string;
    scheme: string;
    effectsTier: "glass" | "eco" | "flat";
    accentMode: "scheme" | "album-art" | "fixed" | "os";
    materialPalette: boolean;
    snippets: string[];
    contentScale?: number;
    navigationScale?: number;
    playbarScale?: number;
    fontFamily?: string;
  };
};

export type CatalogTheme = {
  id: string;
  title: string;
  description?: string;
  schemes: string[];
  className?: string;
  css?: string;
  source?: string;
  commit?: string | null;
  requiresJs?: boolean;
  marketplaceOnly?: boolean;
  supportState?: string;
  lastVerifiedSpotify?: string;
};

export type CatalogSpotxSwitch = {
  id: string;
  configKey: string;
  relatedConfigKeys?: string[];
  cliArguments: string[];
  label: string;
  description: string;
  group: string;
  type: "boolean" | "enum" | "number" | "string";
  default: FeatureValue;
  values?: string[];
  minimum?: number;
  maximum?: number;
  live: false;
};

export type CatalogSpicetifyOption = {
  id: string;
  section: string;
  label: string;
  description: string;
  type: string;
  default: unknown;
  live: boolean;
};

export type CatalogAsset = {
  id: string;
  title: string;
  description?: string;
  source: string;
  commit?: string | null;
  version?: string;
  sha256?: string;
  license?: string;
  supportState?: string;
  lastVerifiedSpotify: string;
  liveToggle?: boolean;
};

export type CustomizationCatalog = {
  schemaVersion: number;
  catalogVersion: string;
  pins: {
    spotifyVersion: string;
    xpuiSha256: string;
    spotxCommit: string;
    spicetifyVersion: string;
    marketplaceVersion: string;
    themesCommit: string;
  };
  featureGroups: string[];
  spotifyFeatures: CatalogFeature[];
  spotxFeatureOverrides: {
    name: string;
    description: string;
    value: FeatureValue;
    mode: "enable" | "disable" | "custom";
    source: string;
    declaredBySpotify: boolean;
  }[];
  spotxSwitches: CatalogSpotxSwitch[];
  spicetifyOptions: CatalogSpicetifyOption[];
  snippets: CatalogSnippet[];
  presets: CatalogPreset[];
  builtInThemes: CatalogTheme[];
  themes: CatalogTheme[];
  extensions: CatalogAsset[];
  customApps: CatalogAsset[];
};

export const CUSTOMIZATION_CATALOG =
  catalogDocument as unknown as CustomizationCatalog;

if (CUSTOMIZATION_CATALOG.schemaVersion !== 1) {
  throw new Error(
    `Unsupported customization catalog schema ${CUSTOMIZATION_CATALOG.schemaVersion}.`,
  );
}

export const CATALOG_SNIPPET_CSS = Object.fromEntries(
  CUSTOMIZATION_CATALOG.snippets.map((snippet) => [snippet.id, snippet.css]),
);

export const CATALOG_THEME_STYLES = Object.fromEntries(
  CUSTOMIZATION_CATALOG.builtInThemes.map((theme) => [
    theme.id,
    { className: theme.className ?? "", css: theme.css ?? "" },
  ]),
);
