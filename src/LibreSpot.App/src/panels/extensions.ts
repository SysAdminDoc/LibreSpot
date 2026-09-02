import {
  CUSTOMIZATION_CATALOG,
  type CatalogAsset,
  type CatalogSpicetifyOption,
  type FeatureValue,
} from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import { InputRow, PanelIntro, Section, ToggleRow, h } from "../surface/ui.ts";

function itemCard(
  name: string,
  kind: "Extension" | "Custom app",
  status: string,
  asset?: CatalogAsset,
  installed = true,
): UiNode {
  const engineItem = name === "librespot-engine.js" || name === "librespot";
  return h(
    "article",
    { className: "librespot-extension-card", key: `${kind}:${name}` },
    h(
      "div",
      { className: "librespot-extension-card__title" },
      h("strong", null, asset?.title ?? name),
      h(
        "span",
        {
          className: installed || engineItem
            ? "librespot-health-dot is-healthy"
            : "librespot-health-dot",
          "aria-hidden": "true",
        },
      ),
    ),
    h("span", { className: "librespot-badge" }, installed ? `${kind} / Installed` : `${kind} / Available`),
    h("p", null, status),
    h(
      "div",
      { className: "librespot-catalog-meta" },
      h("span", null, asset?.source ?? (engineItem ? "SysAdminDoc/LibreSpot" : "Managed by Spicetify")),
      h("span", null, asset?.lastVerifiedSpotify ? `Spotify ${asset.lastVerifiedSpotify}` : "Desktop apply"),
    ),
  );
}

function optionValue(properties: PanelProperties, option: CatalogSpicetifyOption): FeatureValue {
  const saved = properties.snapshot.state.spicetifyOptions[option.id];
  if (saved !== undefined) return saved;
  if (option.id === "current_theme") return Spicetify.Config?.current_theme ?? "";
  if (option.id === "color_scheme") return properties.snapshot.state.scheme;
  if (option.id === "extensions") return properties.snapshot.installedExtensions.join("|");
  if (option.id === "custom_apps") return properties.snapshot.installedCustomApps.join("|");
  if (typeof option.default === "boolean" || typeof option.default === "number" || typeof option.default === "string") {
    return option.default;
  }
  return JSON.stringify(option.default);
}

function saveOption(
  properties: PanelProperties,
  option: CatalogSpicetifyOption,
  value: FeatureValue,
): void {
  void properties.runtime.update(
    (draft) => {
      draft.spicetifyOptions[option.id] = value;
      if (option.id === "color_scheme" && typeof value === "string" && draft.schemes[value]) {
        draft.scheme = value;
      }
      if (option.id === "replace_colors" && typeof value === "boolean") {
        draft.layers.palette = value;
      }
    },
    `${option.label} saved`,
  );
}

function optionControl(
  properties: PanelProperties,
  option: CatalogSpicetifyOption,
): UiNode {
  const value = optionValue(properties, option);
  if (option.type === "boolean") {
    return ToggleRow({
      label: option.label,
      description: option.description,
      checked: Boolean(value),
      badge: option.live ? "Engine bridge" : "Desktop apply",
      onChange: (checked) => {
        saveOption(properties, option, checked);
      },
    });
  }
  return InputRow({
    label: option.label,
    description: `${option.description} ${option.live ? "The matching engine control changes live." : "LibreSpot Desktop applies this value."}`,
    value: typeof value === "boolean" ? String(value) : value,
    type: option.type === "number" ? "number" : "text",
    onChange: (next) => {
      saveOption(properties, option, option.type === "number" ? Number(next) : next);
    },
  });
}

export function ExtensionsPanel(properties: PanelProperties): UiNode {
  const extensions = properties.snapshot.installedExtensions;
  const customApps = properties.snapshot.installedCustomApps;
  const installedExtensions = new Set(extensions);
  const installedApps = new Set(customApps);
  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "extensions" },
    PanelIntro({
      eyebrow: "Installed catalog",
      title: "Extensions",
      body: "See what Spicetify has registered and whether the live engine can manage it now. Installing new files and changing bundle-loaded items stays with LibreSpot Desktop.",
    }),
    Section({
      title: "Companion extension",
      description: "The extension owns state, health checks, live styles, and the always-reachable LibreSpot entry.",
      children: h(
        "div",
        { className: "librespot-extension-grid" },
        itemCard(
          "librespot-engine.js",
          "Extension",
          "Loaded on every Spotify page and responding to live state.",
        ),
      ),
    }),
    Section({
      title: "Extension catalog",
      description: `${extensions.length} installed of ${CUSTOMIZATION_CATALOG.extensions.length} managed extensions. Bundle-loaded changes use LibreSpot Desktop.`,
      children: h(
        "div",
        { className: "librespot-extension-grid" },
        ...CUSTOMIZATION_CATALOG.extensions.map((asset) =>
          itemCard(
            asset.id,
            "Extension",
            installedExtensions.has(asset.id)
              ? "The registered file is present in Spicetify.Config."
              : "Install and register this item through LibreSpot Desktop.",
            asset,
            installedExtensions.has(asset.id),
          ),
        ),
      ),
    }),
    Section({
      title: "Custom app catalog",
      description: `${customApps.length} installed of ${CUSTOMIZATION_CATALOG.customApps.length} managed custom apps. Route health is checked after navigation.`,
      children: h(
        "div",
        { className: "librespot-extension-grid" },
        ...CUSTOMIZATION_CATALOG.customApps.map((asset) =>
          itemCard(
            asset.id,
            "Custom app",
            installedApps.has(asset.id)
              ? "The route is registered and included in the health check."
              : "Install and wire this route through LibreSpot Desktop.",
            asset,
            installedApps.has(asset.id),
          ),
        ),
      ),
    }),
    h(
      "details",
      { className: "librespot-section librespot-disclosure-section" },
      h(
        "summary",
        null,
        h(
          "span",
          { className: "librespot-disclosure-summary" },
          h(
            "span",
            { className: "librespot-disclosure-summary__copy" },
            h("strong", null, "Spicetify configuration"),
            h(
              "span",
              null,
              "Live engine equivalents update now. Bundle settings wait for LibreSpot Desktop.",
            ),
          ),
          h(
            "span",
            { className: "librespot-badge" },
            `${CUSTOMIZATION_CATALOG.spicetifyOptions.length} options`,
          ),
        ),
      ),
      h(
        "div",
        { className: "librespot-section__body" },
        h(
          Spicetify.React.Fragment,
          null,
          ...CUSTOMIZATION_CATALOG.spicetifyOptions.map((option) =>
            h("div", { className: "librespot-feature", key: option.id }, optionControl(properties, option)),
          ),
        ),
      ),
    ),
  );
}
