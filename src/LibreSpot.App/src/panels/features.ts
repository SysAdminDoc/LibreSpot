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
  eventCurrentTarget,
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
  const features = mergedFeatures(properties);
  const [openGroups, setOpenGroups] = React.useState<Record<string, boolean>>(
    () => Object.fromEntries([...GROUP_ORDER, SPOTX_GROUP_KEY].map((group) => [group, false])),
  );
  const normalizedQuery = query.trim().toLowerCase();
  const filtered = normalizedQuery
    ? features.filter(
        (feature) =>
          feature.name.toLowerCase().includes(normalizedQuery) ||
          feature.description.toLowerCase().includes(normalizedQuery) ||
          feature.group.toLowerCase().includes(normalizedQuery) ||
          feature.source?.toLowerCase().includes(normalizedQuery),
      )
    : features;
  const visibleGroupCount = GROUP_ORDER.filter((group) =>
    filtered.some((feature) => feature.group === group),
  ).length;
  const customizedCount = countCustomizedFeatures(
    properties.snapshot.state.featureOverrides,
    features.map((feature) => feature.name),
  );
  const catalogDescription = properties.snapshot.features.length > 0
    ? `${properties.snapshot.features.length} definitions were discovered live. The tested Spotify ${CUSTOMIZATION_CATALOG.pins.spotifyVersion} catalog fills the remaining gaps.`
    : `Tested against Spotify ${CUSTOMIZATION_CATALOG.pins.spotifyVersion}. Changes still apply through this client's live override API.`;
  const resultLabel = normalizedQuery
    ? `${filtered.length} ${filtered.length === 1 ? "match" : "matches"} in ${visibleGroupCount} ${visibleGroupCount === 1 ? "group" : "groups"}`
    : `${features.length} flags in ${visibleGroupCount} groups, ${customizedCount} customized`;

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
        { className: "librespot-search" },
        h("span", null, "Search features"),
        h("input", {
          type: "search",
          "aria-label": "Search features",
          value: query,
          placeholder: "Name, group, source, or Spotify description",
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
        customizedCount > 0
          ? ActionButton({
              label: "Reset custom flags",
              secondary: true,
              onClick: () => {
                void properties.runtime.update(
                  (draft) => {
                    draft.featureOverrides = {};
                  },
                  "Client flag overrides reset",
                );
              },
            })
          : null,
      ),
    ),
    filtered.length === 0
      ? h(
          "div",
          { className: "librespot-empty-state", role: "status" },
          h("strong", null, "No matching features"),
          h("p", null, "Try a flag name, category, source, or a word from Spotify's description."),
          ActionButton({
            label: "Clear search",
            secondary: true,
            onClick: () => {
              setQuery("");
            },
          }),
        )
      : null,
    ...GROUP_ORDER.flatMap((group) => {
      const groupFeatures = filtered.filter((feature) => feature.group === group);
      if (groupFeatures.length === 0) {
        return [];
      }
      const groupCustomizedCount = groupFeatures.filter(
        (feature) =>
          properties.snapshot.state.featureOverrides[feature.name] !== undefined,
      ).length;
      const groupCountLabel = `${groupFeatures.length} ${groupFeatures.length === 1 ? "flag" : "flags"}${groupCustomizedCount > 0 ? `, ${groupCustomizedCount} custom` : ""}`;
      return [
        h(
          "details",
          {
            className: "librespot-feature-group",
            key: group,
            open: normalizedQuery ? true : Boolean(openGroups[group]),
            onToggle: (event: unknown) => {
              const target = eventCurrentTarget(event);
              if (
                normalizedQuery ||
                !(target instanceof HTMLDetailsElement)
              ) {
                return;
              }
              const next = target.open;
              setOpenGroups((current) =>
                current[group] === next
                  ? current
                  : { ...current, [group]: next },
              );
            },
          },
          h(
            "summary",
            { className: "librespot-feature-group__summary" },
            h(
              "span",
              { className: "librespot-feature-group__copy" },
              h("strong", null, group),
              h(
                "span",
                null,
                catalogDescription,
              ),
            ),
            h(
              "span",
              { className: "librespot-feature-group__count" },
              groupCountLabel,
            ),
          ),
          h(
            "div",
            { className: "librespot-feature-group__body" },
            ...groupFeatures.map((feature) =>
              h(
                "div",
                { className: "librespot-feature", key: feature.name },
                featureControl(properties, feature),
              ),
            ),
          ),
        ),
      ];
    }),
    h(
      "details",
      {
        className: "librespot-feature-group",
        open: Boolean(openGroups[SPOTX_GROUP_KEY]),
        onToggle: (event: unknown) => {
          const target = eventCurrentTarget(event);
          if (
            !(target instanceof HTMLDetailsElement)
          ) {
            return;
          }
          const next = target.open;
          setOpenGroups((current) =>
            current[SPOTX_GROUP_KEY] === next
              ? current
              : { ...current, [SPOTX_GROUP_KEY]: next },
          );
        },
      },
      h(
        "summary",
        { className: "librespot-feature-group__summary" },
        h(
          "span",
          { className: "librespot-feature-group__copy" },
          h("strong", null, SPOTX_GROUP_KEY),
          h(
            "span",
            null,
            "Staged for LibreSpot Desktop and applied with the next binary patch.",
          ),
        ),
        h(
          "span",
          { className: "librespot-feature-group__count" },
          `${SURFACE_SPOTX_SEEDS.length} settings`,
        ),
      ),
      h(
        "div",
        { className: "librespot-feature-group__body" },
        ...SURFACE_SPOTX_SEEDS.map((control) => spotxControl(properties, control)),
      ),
    ),
  );
}
