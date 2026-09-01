import type { ColorScheme, EngineState } from "../core/index.ts";

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

export type SnippetDefinition = {
  id: string;
  title: string;
  description: string;
  category: string;
  css: string;
  source: string;
  lastVerifiedSpotify: string;
};

export const SURFACE_SNIPPETS: readonly SnippetDefinition[] = [
  {
    id: "rounded-covers",
    title: "Rounded cover art",
    description: "Rounds playlist and album artwork without changing cards.",
    category: "Cover art",
    css: '[data-testid="cover-art-image"], .main-image-image { border-radius: var(--librespot-radius) !important; }',
    source: "https://github.com/spicetify/marketplace",
    lastVerifiedSpotify: "1.2.93",
  },
  {
    id: "compact-track-rows",
    title: "Compact track rows",
    description: "Reduces track-list padding while keeping controls usable.",
    category: "Layout",
    css: '.main-trackList-trackListRow { --row-height: 40px; min-height: var(--row-height); }',
    source: "https://github.com/Comfy-Themes/Spicetify",
    lastVerifiedSpotify: "1.2.93",
  },
  {
    id: "quiet-scrollbars",
    title: "Quiet scrollbars",
    description: "Keeps scrollbars narrow until the pointer reaches them.",
    category: "Window",
    css: '.os-scrollbar-handle { opacity: .55; } .os-scrollbar:hover .os-scrollbar-handle { opacity: 1; }',
    source: "https://github.com/spicetify/marketplace",
    lastVerifiedSpotify: "1.2.93",
  },
] as const;

export type PresetDefinition = {
  id: string;
  title: string;
  description: string;
  apply(draft: EngineState): void;
};

export const SURFACE_PRESETS: readonly PresetDefinition[] = [
  {
    id: "oled",
    title: "OLED",
    description: "Black surfaces, restrained effects, and artwork accent.",
    apply: (draft) => {
      draft.name = "OLED";
      draft.scheme = "OLED";
      draft.effectsTier = "eco";
      draft.dynamicAccent.mode = "album-art";
    },
  },
  {
    id: "accessibility",
    title: "Accessibility",
    description: "High contrast, flat surfaces, large targets, and thick focus.",
    apply: (draft) => {
      draft.name = "Accessibility";
      draft.scheme = "HighContrast";
      draft.effectsTier = "flat";
      draft.layers.accessibility = true;
      draft.appearance.scale.content = 1.12;
      draft.appearance.scale.navigation = 1.1;
      draft.appearance.fontFamily = "Atkinson Hyperlegible, SpotifyMixUI, sans-serif";
    },
  },
  {
    id: "compact",
    title: "Compact",
    description: "Dense rows, a smaller rail, and a low-profile playbar.",
    apply: (draft) => {
      draft.name = "Compact";
      draft.effectsTier = "eco";
      draft.enabledSnippets = [
        ...new Set([...draft.enabledSnippets, "compact-track-rows"]),
      ];
      draft.appearance.scale.navigation = 0.9;
      draft.appearance.scale.content = 0.92;
      draft.appearance.scale.playbar = 0.9;
    },
  },
  {
    id: "performance",
    title: "Performance",
    description: "No blur or motion, with dynamic palette work disabled.",
    apply: (draft) => {
      draft.name = "Performance";
      draft.effectsTier = "flat";
      draft.autoEffects = false;
      draft.dynamicAccent.mode = "scheme";
      draft.dynamicAccent.materialPalette = false;
    },
  },
] as const;

export const SURFACE_SNIPPET_CSS = Object.fromEntries(
  SURFACE_SNIPPETS.map((snippet) => [snippet.id, snippet.css]),
);

export type ClientFeatureSeed = {
  name: string;
  description: string;
  type: "bool" | "enum";
  default: boolean | string;
  values?: string[];
  group: string;
  serverGated?: boolean;
};

export const SURFACE_FEATURE_SEEDS: readonly ClientFeatureSeed[] = [
  {
    name: "enableGlobalNavBar",
    description: "Use Spotify's global navigation bar layout.",
    type: "bool",
    default: true,
    group: "Layout",
  },
  {
    name: "enableLyricsFullscreen",
    description: "Expose the full-screen lyrics presentation.",
    type: "bool",
    default: true,
    group: "Lyrics",
  },
  {
    name: "enableNowPlayingView",
    description: "Use the right-side Now Playing view.",
    type: "bool",
    default: true,
    group: "Now Playing",
  },
  {
    name: "enableEnhanceLikedSongs",
    description: "Show Enhance controls when the account and server allow them.",
    type: "bool",
    default: false,
    group: "Library",
    serverGated: true,
  },
  {
    name: "enableJam",
    description: "Show Jam entry points when the account and server allow them.",
    type: "bool",
    default: false,
    group: "Playback",
    serverGated: true,
  },
  {
    name: "homeStructure",
    description: "Select the Home shelf structure supplied by Spotify.",
    type: "enum",
    default: "default",
    values: ["default", "compact", "expanded"],
    group: "Home",
  },
] as const;

export type SpotXControlSeed = {
  id: string;
  label: string;
  description: string;
  group: string;
  default: boolean;
};

export const SURFACE_SPOTX_SEEDS: readonly SpotXControlSeed[] = [
  {
    id: "adblock",
    label: "Ad blocking",
    description: "Pass SpotX's pinned ad patch switches through on the next desktop apply.",
    group: "Ads and tracking",
    default: true,
  },
  {
    id: "podcasts-off",
    label: "Hide podcasts",
    description: "Hide podcast surfaces through SpotX on the next desktop apply.",
    group: "Home",
    default: false,
  },
  {
    id: "audiobooks-off",
    label: "Hide audiobooks",
    description: "Hide audiobook surfaces through SpotX on the next desktop apply.",
    group: "Library",
    default: false,
  },
  {
    id: "lyrics",
    label: "Lyrics patch",
    description: "Keep the pinned SpotX lyrics patch enabled after the next desktop apply.",
    group: "Lyrics",
    default: true,
  },
  {
    id: "block-update",
    label: "Block Spotify updates",
    description: "Pass the update-blocking switch to SpotX on the next desktop apply.",
    group: "Everything else",
    default: true,
  },
  {
    id: "telemetry-off",
    label: "Reduce telemetry",
    description: "Pass SpotX telemetry switches through on the next desktop apply.",
    group: "Ads and tracking",
    default: true,
  },
] as const;
