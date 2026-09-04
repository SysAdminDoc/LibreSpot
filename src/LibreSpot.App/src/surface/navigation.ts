export type PanelId =
  | "store"
  | "look"
  | "tweaks"
  | "features"
  | "presets"
  | "health";

export type PanelDefinition = {
  id: PanelId;
  label: string;
  description: string;
  icon: "store" | "look" | "tweaks" | "features" | "presets" | "health";
};

export const PANEL_DEFINITIONS: readonly PanelDefinition[] = [
  {
    id: "store",
    label: "Store",
    description: "Themes, extensions, and apps",
    icon: "store",
  },
  {
    id: "look",
    label: "Look",
    description: "Palette, layers, effects, scale, and schedule",
    icon: "look",
  },
  {
    id: "tweaks",
    label: "Tweaks",
    description: "Reviewed CSS and page arrangement",
    icon: "tweaks",
  },
  {
    id: "features",
    label: "Features",
    description: "Spotify flags and SpotX switches",
    icon: "features",
  },
  {
    id: "presets",
    label: "Presets",
    description: "Built-in and saved profiles",
    icon: "presets",
  },
  {
    id: "health",
    label: "Health",
    description: "Anchors, routes, versions, and diagnostics",
    icon: "health",
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
  if (!segment || segment === "extensions" || segment === "marketplace") {
    return "store";
  }
  return PANEL_DEFINITIONS.some((panel) => panel.id === segment)
    ? (segment as PanelId)
    : "store";
}
