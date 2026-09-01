export type PanelId =
  | "look"
  | "tweaks"
  | "features"
  | "extensions"
  | "presets"
  | "health";

export type PanelDefinition = {
  id: PanelId;
  label: string;
  description: string;
};

export const PANEL_DEFINITIONS: readonly PanelDefinition[] = [
  {
    id: "look",
    label: "Look",
    description: "Palette, layers, effects, scale, and schedule",
  },
  {
    id: "tweaks",
    label: "Tweaks",
    description: "Reviewed CSS and page arrangement",
  },
  {
    id: "features",
    label: "Features",
    description: "Spotify flags and SpotX switches",
  },
  {
    id: "extensions",
    label: "Extensions",
    description: "Installed items, enabled state, and health",
  },
  {
    id: "presets",
    label: "Presets",
    description: "Built-in and saved profiles",
  },
  {
    id: "health",
    label: "Health",
    description: "Anchors, routes, versions, and diagnostics",
  },
] as const;

export function panelPath(panel: PanelId): string {
  return `/librespot/${panel}`;
}

export function panelFromPath(pathname: string): PanelId {
  const segment = pathname
    .replace(/^\/librespot\/?/, "")
    .split("/")[0]
    ?.toLowerCase();
  return PANEL_DEFINITIONS.some((panel) => panel.id === segment)
    ? (segment as PanelId)
    : "look";
}
