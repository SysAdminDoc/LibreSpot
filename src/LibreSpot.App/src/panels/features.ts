import searchIcon from "lucide-static/icons/search.svg";
import {
  CUSTOMIZATION_CATALOG,
  type CatalogFeature,
  type CatalogSpotxSwitch,
  type CapturedFeature,
  type EngineState,
  type FeatureValue,
} from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import {
  SURFACE_FEATURE_SEEDS,
  SURFACE_SPOTX_SEEDS,
} from "../surface/builtins.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import {
  ActionButton,
  InputRow,
  PanelIntro,
  SelectRow,
  ToggleRow,
  eventTarget,
  h,
} from "../surface/ui.ts";

type DisplayFeature = CapturedFeature & {
  group: string;
  serverGated: boolean;
  source?: string;
  spotxForced?: CatalogFeature["spotxForced"];
};

const GROUP_ORDER = CUSTOMIZATION_CATALOG.featureGroups;
const SPOTX_GROUP_KEY = "SpotX switches";
type FeatureFilter = "all" | "live" | "desktop" | "custom";

const FEATURE_FILTERS: readonly { id: FeatureFilter; label: string }[] = [
  { id: "all", label: "All" },
  { id: "live", label: "Live in Spotify" },
  { id: "desktop", label: "Desktop reapply" },
  { id: "custom", label: "Customized" },
];

function searchGlyph(): UiNode {
  return h("span", {
    className: "librespot-feature-search__icon",
    "aria-hidden": "true",
    dangerouslySetInnerHTML: { __html: searchIcon },
  });
}

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
      ...(seed.minimum === undefined ? {} : { minimum: seed.minimum }),
      ...(seed.maximum === undefined ? {} : { maximum: seed.maximum }),
      group: seed.group,
      serverGated: seed.serverGated,
      source: seed.source,
      ...(seed.spotxForced ? { spotxForced: seed.spotxForced } : {}),
    });
  }
  for (const captured of properties.snapshot.features) {
    const seed = SURFACE_FEATURE_SEEDS.find((item) => item.name === captured.name);
    features.set(captured.name, {
      ...captured,
      group: seed?.group ?? inferredGroup(captured.name),
      serverGated: seed?.serverGated ?? false,
      ...(seed?.source ? { source: seed.source } : {}),
      ...(seed?.spotxForced ? { spotxForced: seed.spotxForced } : {}),
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

/**
 * True when a flag is holding a value the user set rather than Spotify's. The
 * marker and the revert control both hang off this, so the row cannot show one
 * without the other.
 */
export function isCustomizedFeature(
  overrides: Readonly<Record<string, FeatureValue>>,
  name: string,
): boolean {
  return overrides[name] !== undefined;
}

/** Overrides with one flag removed, which is what restores Spotify's own value. */
export function withFeatureReverted(
  overrides: Readonly<Record<string, FeatureValue>>,
  name: string,
): Record<string, FeatureValue> {
  return Object.fromEntries(
    Object.entries(overrides).filter(([key]) => key !== name),
  );
}

/** How many flags in this catalog are customized, for the summary line. */
export function countCustomizedFeatures(
  overrides: Readonly<Record<string, FeatureValue>>,
  names: readonly string[],
): number {
  return names.filter((name) => isCustomizedFeature(overrides, name)).length;
}

function revertFeature(properties: PanelProperties, name: string): void {
  // Removing the key is what restores the value Spotify sent: the override
  // resolver looks up the remote value for every entry that disappeared.
  void properties.runtime.update(
    (draft) => {
      draft.featureOverrides = withFeatureReverted(draft.featureOverrides, name);
    },
    `${name} back to Spotify's value`,
  );
}

function featureControl(
  properties: PanelProperties,
  feature: DisplayFeature,
): UiNode {
  const stored = properties.snapshot.state.featureOverrides[feature.name];
  const value = stored ?? feature.default;
  const isCustom = isCustomizedFeature(properties.snapshot.state.featureOverrides, feature.name);
  const badge = isCustom
    ? "Custom"
    : feature.spotxForced
      ? `SpotX ${feature.spotxForced.mode}`
      : feature.serverGated
        ? "Account gated"
        : "Live";
  // Only a changed flag gets a revert control, so the row reads as default until
  // it is not.
  const action = isCustom
    ? ActionButton({
        label: "Revert",
        secondary: true,
        accessibleLabel: `Revert ${feature.name} to Spotify's value`,
        onClick: () => {
          revertFeature(properties, feature.name);
        },
      })
    : null;
  const description = `${feature.description}${feature.serverGated ? " It may do nothing on a free account." : ""}${feature.spotxForced ? ` SpotX pins the default from ${feature.spotxForced.source}.` : ""}`;
  if (feature.type === "enum" && feature.values) {
    return SelectRow({
      label: feature.name,
      description,
      value: String(value),
      options: feature.values.map((option) => ({ value: option, label: option })),
      badge,
      ...(action ? { action } : {}),
      onChange: (next) => {
        setFeature(properties, feature.name, next);
      },
    });
  }
  if (feature.type === "number") {
    return InputRow({
      label: feature.name,
      description,
      value: typeof value === "number" ? value : Number(value),
      type: "number",
      ...(feature.minimum === undefined ? {} : { min: feature.minimum }),
      ...(feature.maximum === undefined ? {} : { max: feature.maximum }),
      badge,
      ...(action ? { action } : {}),
      onChange: (next) => {
        const number = Number(next);
        if (Number.isFinite(number)) setFeature(properties, feature.name, number);
      },
    });
  }
  if (feature.type === "string") {
    return InputRow({
      label: feature.name,
      description,
      value: String(value),
      type: "text",
      badge,
      ...(action ? { action } : {}),
      onChange: (next) => {
        setFeature(properties, feature.name, next);
      },
    });
  }
  return ToggleRow({
    label: feature.name,
    description,
    checked: Boolean(value),
    badge,
    ...(action ? { action } : {}),
    onChange: (checked) => {
      setFeature(properties, feature.name, checked);
    },
  });
}

function saveSpotx(
  properties: PanelProperties,
  control: CatalogSpotxSwitch,
  value: FeatureValue,
): void {
  void properties.runtime.update(
    (draft: EngineState) => {
      draft.spotxSwitches[control.configKey] = value;
    },
    `${control.label} saved for desktop import`,
  );
}

function spotxControl(
  properties: PanelProperties,
  control: CatalogSpotxSwitch,
): UiNode {
  const value = properties.snapshot.state.spotxSwitches[control.configKey] ?? control.default;
  if (control.type === "enum" && control.values) {
    return SelectRow({
      label: control.label,
      description: control.description,
      value: String(value),
      options: control.values.map((option) => ({ value: option, label: option || "Automatic" })),
      onChange: (next) => {
        saveSpotx(properties, control, next);
      },
    });
  }
  if (control.type === "number" || control.type === "string") {
    return InputRow({
      label: control.label,
      description: control.description,
      value: value as string | number,
      type: control.type === "number" ? "number" : "text",
      ...(control.minimum === undefined ? {} : { min: control.minimum }),
      ...(control.maximum === undefined ? {} : { max: control.maximum }),
      onChange: (next) => {
        const resolved = control.type === "number" ? Number(next) : next;
        if (typeof resolved === "string" || Number.isFinite(resolved)) {
          saveSpotx(properties, control, resolved);
        }
      },
    });
  }
  return ToggleRow({
    label: control.label,
    description: control.description,
    badge: "Desktop apply",
    checked: Boolean(value),
    onChange: (checked) => {
      saveSpotx(properties, control, checked);
    },
  });
}

export function FeaturesPanel(properties: PanelProperties): UiNode {
  const React = Spicetify.React;
  const [query, setQuery] = React.useState("");
  const [filter, setFilter] = React.useState<FeatureFilter>("all");
  const [selectedGroup, setSelectedGroup] = React.useState<string>("Playback");
  const features = mergedFeatures(properties);
  const normalizedQuery = query.trim().toLowerCase();
  const customizedCount = countCustomizedFeatures(
    properties.snapshot.state.featureOverrides,
    features.map((feature) => feature.name),
  );
  const customizedSpotxCount = SURFACE_SPOTX_SEEDS.filter((control) => {
    const saved = properties.snapshot.state.spotxSwitches[control.configKey];
    return saved !== undefined && saved !== control.default;
  }).length;
  const queryMatchesFeature = (feature: DisplayFeature): boolean =>
    !normalizedQuery ||
    feature.name.toLowerCase().includes(normalizedQuery) ||
    feature.description.toLowerCase().includes(normalizedQuery) ||
    feature.group.toLowerCase().includes(normalizedQuery) ||
    feature.source?.toLowerCase().includes(normalizedQuery) === true;
  const queryMatchesSpotx = (control: CatalogSpotxSwitch): boolean =>
    !normalizedQuery ||
    control.label.toLowerCase().includes(normalizedQuery) ||
    control.description.toLowerCase().includes(normalizedQuery) ||
    control.configKey.toLowerCase().includes(normalizedQuery);
  const filteredFeatures = features.filter((feature) =>
    queryMatchesFeature(feature) &&
    filter !== "desktop" &&
    (filter !== "custom" || isCustomizedFeature(properties.snapshot.state.featureOverrides, feature.name)),
  );
  const filteredSpotx = SURFACE_SPOTX_SEEDS.filter((control) => {
    const isCustom = properties.snapshot.state.spotxSwitches[control.configKey] !== undefined &&
      properties.snapshot.state.spotxSwitches[control.configKey] !== control.default;
    return queryMatchesSpotx(control) &&
      filter !== "live" &&
      (filter !== "custom" || isCustom);
  });
  const availableGroups = [
    ...GROUP_ORDER.filter((group) =>
      filteredFeatures.some((feature) => feature.group === group),
    ),
    ...(filteredSpotx.length > 0 ? [SPOTX_GROUP_KEY] : []),
  ];
  const activeGroup = availableGroups.includes(selectedGroup)
    ? selectedGroup
    : availableGroups[0] ?? "";
  const activeFeatures = filteredFeatures.filter((feature) => feature.group === activeGroup);
  const activeSpotx = activeGroup === SPOTX_GROUP_KEY ? filteredSpotx : [];
  const visibleCount = filteredFeatures.length + filteredSpotx.length;
  const catalogDescription = properties.snapshot.features.length > 0
    ? `${properties.snapshot.features.length} definitions were discovered live. The tested Spotify ${CUSTOMIZATION_CATALOG.pins.spotifyVersion} catalog fills the remaining gaps.`
    : `Tested against Spotify ${CUSTOMIZATION_CATALOG.pins.spotifyVersion}. Changes still apply through this client's live override API.`;
  const filterCounts: Record<FeatureFilter, number> = {
    all: features.length + SURFACE_SPOTX_SEEDS.length,
    live: features.length,
    desktop: SURFACE_SPOTX_SEEDS.length,
    custom: customizedCount + customizedSpotxCount,
  };
  const resultLabel = `${visibleCount} ${visibleCount === 1 ? "control" : "controls"} in ${availableGroups.length} ${availableGroups.length === 1 ? "group" : "groups"}`;

  const onSearch = (event: unknown) => {
    const target = eventTarget(event);
    if (target instanceof HTMLInputElement) {
      setQuery(target.value);
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
      "div",
      { className: "librespot-feature-toolbar" },
      h(
        "label",
        { className: "librespot-feature-search" },
        searchGlyph(),
        h("span", { className: "librespot-visually-hidden" }, "Search flags and switches"),
        h("input", {
          type: "search",
          "aria-label": "Search flags and switches",
          value: query,
          placeholder: "Search flags and switches",
          onInput: onSearch,
        }),
      ),
      h(
        "div",
        { className: "librespot-feature-toolbar__status" },
        h("span", { "aria-live": "polite" }, resultLabel),
        normalizedQuery
          ? ActionButton({
              label: "Clear search",
              secondary: true,
              onClick: () => {
                setQuery("");
              },
            })
          : null,
        customizedCount + customizedSpotxCount > 0
          ? ActionButton({
              label: "Reset customized",
              secondary: true,
              onClick: () => {
                void properties.runtime.update(
                  (draft) => {
                    draft.featureOverrides = {};
                    draft.spotxSwitches = {};
                  },
                  "Customized feature values reset",
                );
              },
            })
          : null,
      ),
    ),
    h(
      "div",
      { className: "librespot-feature-filters", role: "group", "aria-label": "Feature source" },
      ...FEATURE_FILTERS.map((item) => h(
        "button",
        {
          type: "button",
          key: item.id,
          className: filter === item.id ? "is-active" : "",
          "aria-pressed": String(filter === item.id),
          onClick: () => setFilter(item.id),
        },
        h("span", { className: `librespot-feature-filter-dot is-${item.id}`, "aria-hidden": "true" }),
        h("strong", null, item.label),
        h("span", null, String(filterCounts[item.id])),
      )),
    ),
    availableGroups.length === 0
      ? h(
          "div",
          { className: "librespot-empty-state", role: "status" },
          h("strong", null, "No matching features"),
          h("p", null, "Try another name, source, or filter."),
          ActionButton({
            label: "Clear filters",
            secondary: true,
            onClick: () => {
              setQuery("");
              setFilter("all");
            },
          }),
        )
      : null,
    activeGroup
      ? h(
          "div",
          { className: "librespot-feature-workspace" },
          h(
            "nav",
            { className: "librespot-feature-groups", "aria-label": "Feature groups" },
            ...availableGroups.map((group) => {
              const count = group === SPOTX_GROUP_KEY
                ? filteredSpotx.length
                : filteredFeatures.filter((feature) => feature.group === group).length;
              return h(
                "button",
                {
                  type: "button",
                  key: group,
                  className: activeGroup === group ? "is-active" : "",
                  "aria-current": activeGroup === group ? "page" : undefined,
                  onClick: () => setSelectedGroup(group),
                },
                h("span", null, group),
                h("strong", null, String(count)),
              );
            }),
          ),
          h(
            "section",
            { className: "librespot-feature-group", "aria-labelledby": "librespot-feature-group-title" },
            h(
              "div",
              { className: "librespot-feature-group__summary" },
              h(
                "span",
                { className: "librespot-feature-group__copy" },
                h("strong", { id: "librespot-feature-group-title" }, activeGroup),
                h(
                  "span",
                  null,
                  activeGroup === SPOTX_GROUP_KEY
                    ? "Saved for LibreSpot Desktop and applied with the next patch."
                    : catalogDescription,
                ),
              ),
              h(
                "span",
                { className: "librespot-feature-group__count" },
                `${activeFeatures.length + activeSpotx.length} ${activeGroup === SPOTX_GROUP_KEY ? "settings" : "flags"}`,
              ),
            ),
            h(
              "div",
              { className: "librespot-feature-group__body" },
              ...activeFeatures.map((feature) => h(
                "div",
                { className: "librespot-feature", key: feature.name },
                featureControl(properties, feature),
              )),
              ...activeSpotx.map((control) => h(
                "div",
                { className: "librespot-feature", key: control.configKey },
                spotxControl(properties, control),
              )),
            ),
          ),
        )
      : null,
  );
}
