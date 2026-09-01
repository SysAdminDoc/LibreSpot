import type {
  CapturedFeature,
  EngineState,
  FeatureValue,
} from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import {
  SURFACE_FEATURE_SEEDS,
  SURFACE_SPOTX_SEEDS,
} from "../surface/builtins.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import { PanelIntro, Section, SelectRow, ToggleRow, h } from "../surface/ui.ts";

type DisplayFeature = CapturedFeature & {
  group: string;
  serverGated: boolean;
};

const GROUP_ORDER = [
  "Playback",
  "Now Playing",
  "Library",
  "Home",
  "Lyrics",
  "Layout",
  "Ads and tracking",
  "Fun",
  "Everything else",
] as const;

function inferredGroup(name: string): string {
  const value = name.toLowerCase();
  if (value.includes("play") || value.includes("queue") || value.includes("audio")) {
    return "Playback";
  }
  if (value.includes("nowplaying") || value.includes("now_playing")) {
    return "Now Playing";
  }
  if (value.includes("library") || value.includes("collection")) {
    return "Library";
  }
  if (value.includes("home")) {
    return "Home";
  }
  if (value.includes("lyric")) {
    return "Lyrics";
  }
  if (value.includes("layout") || value.includes("nav") || value.includes("sidebar")) {
    return "Layout";
  }
  if (value.includes("ad") || value.includes("telemetry") || value.includes("track")) {
    return "Ads and tracking";
  }
  return "Everything else";
}

function mergedFeatures(properties: PanelProperties): DisplayFeature[] {
  const features = new Map<string, DisplayFeature>();
  for (const seed of SURFACE_FEATURE_SEEDS) {
    features.set(seed.name, {
      name: seed.name,
      description: seed.description,
      type: seed.type,
      default: seed.default,
      ...(seed.values ? { values: [...seed.values] } : {}),
      group: seed.group,
      serverGated: seed.serverGated ?? false,
    });
  }
  for (const captured of properties.snapshot.features) {
    const seed = SURFACE_FEATURE_SEEDS.find((item) => item.name === captured.name);
    features.set(captured.name, {
      ...captured,
      group: seed?.group ?? inferredGroup(captured.name),
      serverGated: seed?.serverGated ?? false,
    });
  }
  return [...features.values()].sort((left, right) =>
    left.name.localeCompare(right.name),
  );
}

function setFeature(
  properties: PanelProperties,
  name: string,
  value: FeatureValue,
): void {
  void properties.runtime.update(
    (draft) => {
      draft.featureOverrides[name] = value;
    },
    `${name} applied live`,
  );
}

function featureControl(
  properties: PanelProperties,
  feature: DisplayFeature,
): UiNode {
  const stored = properties.snapshot.state.featureOverrides[feature.name];
  const value = stored ?? feature.default;
  const badge = feature.serverGated ? "Account gated" : "Live";
  if (feature.type === "enum" && feature.values) {
    return SelectRow({
      label: feature.name,
      description: `${feature.description}${feature.serverGated ? " It may do nothing on a free account." : ""}`,
      value: String(value),
      options: feature.values.map((option) => ({ value: option, label: option })),
      onChange: (next) => {
        setFeature(properties, feature.name, next);
      },
    });
  }
  return ToggleRow({
    label: feature.name,
    description: `${feature.description}${feature.serverGated ? " It may do nothing on a free account." : ""}`,
    checked: Boolean(value),
    badge,
    onChange: (checked) => {
      setFeature(properties, feature.name, checked);
    },
  });
}

export function FeaturesPanel(properties: PanelProperties): UiNode {
  const React = Spicetify.React;
  const [query, setQuery] = React.useState("");
  const features = mergedFeatures(properties);
  const normalizedQuery = query.trim().toLowerCase();
  const filtered = normalizedQuery
    ? features.filter(
        (feature) =>
          feature.name.toLowerCase().includes(normalizedQuery) ||
          feature.description.toLowerCase().includes(normalizedQuery),
      )
    : features;

  const onSearch = (event: unknown) => {
    if (event instanceof Event && event.target instanceof HTMLInputElement) {
      setQuery(event.target.value);
    }
  };

  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "features" },
    PanelIntro({
      eyebrow: "Runtime and patch controls",
      title: "Features",
      body: "Client flags use Spotify's own override APIs and change live. SpotX switches are saved into the same profile, then handed to the desktop because binary patches need a reapply.",
    }),
    h(
      "label",
      { className: "librespot-search" },
      h("span", null, "Search features"),
      h("input", {
        type: "search",
        value: query,
        placeholder: "Name or Spotify description",
        onInput: onSearch,
      }),
    ),
    ...GROUP_ORDER.flatMap((group) => {
      const groupFeatures = filtered.filter((feature) => feature.group === group);
      if (groupFeatures.length === 0) {
        return [];
      }
      return [
        Section({
          title: group,
          description: `${groupFeatures.length} client-side ${groupFeatures.length === 1 ? "flag" : "flags"} visible in this build.`,
          children: h(
            Spicetify.React.Fragment,
            null,
            ...groupFeatures.map((feature) =>
              h(
                "div",
                { className: "librespot-feature", key: feature.name },
                featureControl(properties, feature),
              ),
            ),
          ),
        }),
      ];
    }),
    Section({
      title: "SpotX switches",
      description: "These values are staged for LibreSpot Desktop. They do not claim to change the already-patched binary.",
      children: h(
        Spicetify.React.Fragment,
        null,
        ...SURFACE_SPOTX_SEEDS.map((control) =>
          ToggleRow({
            label: control.label,
            description: control.description,
            badge: "Desktop apply",
            checked: Boolean(
              properties.snapshot.state.spotxSwitches[control.id] ??
                control.default,
            ),
            onChange: (checked) => {
              void properties.runtime.update(
                (draft: EngineState) => {
                  draft.spotxSwitches[control.id] = checked;
                },
                `${control.label} saved for desktop import`,
              );
            },
          }),
        ),
      ),
    }),
  );
}
