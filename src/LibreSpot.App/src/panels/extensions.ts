import type { UiNode } from "../spicetify-globals.d.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import { PanelIntro, Section, h } from "../surface/ui.ts";

function itemCard(
  name: string,
  kind: "Extension" | "Custom app",
  status: string,
): UiNode {
  const engineItem = name === "librespot-engine.js" || name === "librespot";
  return h(
    "article",
    { className: "librespot-extension-card", key: `${kind}:${name}` },
    h(
      "div",
      { className: "librespot-extension-card__title" },
      h("strong", null, name),
      h(
        "span",
        {
          className: engineItem
            ? "librespot-health-dot is-healthy"
            : "librespot-health-dot",
          "aria-hidden": "true",
        },
      ),
    ),
    h("span", { className: "librespot-badge" }, kind),
    h("p", null, status),
    h(
      "div",
      { className: "librespot-catalog-meta" },
      h("span", null, engineItem ? "Verified on Spotify 1.2.93" : "Managed by Spicetify"),
      h("span", null, engineItem ? "Live" : "Desktop apply"),
    ),
  );
}

export function ExtensionsPanel(properties: PanelProperties): UiNode {
  const extensions = properties.snapshot.installedExtensions;
  const customApps = properties.snapshot.installedCustomApps;
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
      title: "Registered extensions",
      description:
        extensions.length === 0
          ? "No additional extensions are reported by Spicetify.Config."
          : `${extensions.length} extensions are registered in this Spotify bundle.`,
      children: h(
        "div",
        { className: "librespot-extension-grid" },
        ...(extensions.length > 0
          ? extensions.map((name) =>
              itemCard(
                name,
                "Extension",
                "Changing this bundle-loaded extension needs a desktop apply.",
              ),
            )
          : [h("p", { className: "librespot-empty" }, "Nothing else is installed.")]),
      ),
    }),
    Section({
      title: "Registered custom apps",
      description:
        customApps.length === 0
          ? "No custom apps are reported by Spicetify.Config."
          : `${customApps.length} custom apps are registered in this Spotify bundle.`,
      children: h(
        "div",
        { className: "librespot-extension-grid" },
        ...(customApps.length > 0
          ? customApps.map((name) =>
              itemCard(
                name,
                "Custom app",
                name === "librespot"
                  ? "This surface is loaded and its route is under test."
                  : "Route health is checked after every navigation.",
              ),
            )
          : [h("p", { className: "librespot-empty" }, "Nothing is registered.")]),
      ),
    }),
  );
}
