import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  CUSTOMIZATION_CATALOG,
  contrastRatio,
  readableText,
  validateSchemeContrast,
} from "../src/core/index.ts";
import { countInstalledManagedAssets } from "../src/panels/extensions.ts";
import { updateSnippetSelection } from "../src/panels/tweaks.ts";
import {
  BUILTIN_SCHEMES,
  SURFACE_PRESETS,
  SURFACE_SNIPPETS,
} from "../src/surface/builtins.ts";
import {
  PANEL_DEFINITIONS,
  panelFromPath,
  panelPath,
} from "../src/surface/navigation.ts";
import { displaySchemeName } from "../src/surface/labels.ts";
import {
  eventCurrentTarget,
  eventTarget,
} from "../src/surface/ui.ts";

describe("LibreSpot surface contract", () => {
  it("keeps the six panels in the requested mental order", () => {
    expect(PANEL_DEFINITIONS.map((panel) => panel.id)).toEqual([
      "look",
      "tweaks",
      "features",
      "extensions",
      "presets",
      "health",
    ]);
    for (const panel of PANEL_DEFINITIONS) {
      expect(panelFromPath(panelPath(panel.id))).toBe(panel.id);
    }
    expect(panelFromPath("/librespot")).toBe("look");
  });

  it("ships all four Prism schemes with readable text and controls", () => {
    expect(Object.keys(BUILTIN_SCHEMES)).toEqual([
      "Dark",
      "Light",
      "OLED",
      "HighContrast",
    ]);
    for (const scheme of Object.values(BUILTIN_SCHEMES)) {
      expect(validateSchemeContrast(scheme)).toEqual([]);
      const button = scheme.button ?? "1ED760";
      expect(contrastRatio(readableText(button), button)).toBeGreaterThanOrEqual(
        4.5,
      );
    }
  });

  it("keeps internal scheme ids out of human-facing labels", () => {
    expect(displaySchemeName("HighContrast")).toBe("High contrast");
    expect(displaySchemeName("OLED")).toBe("OLED");
  });

  it("accepts React synthetic event shapes for form controls", () => {
    const input = document.createElement("input");
    const details = document.createElement("details");
    expect(eventTarget({ target: input })).toBe(input);
    expect(eventCurrentTarget({ currentTarget: details })).toBe(details);
    expect(eventTarget(new Event("input"))).toBeNull();
  });

  it("provides the four named starting presets", () => {
    expect(SURFACE_PRESETS.map((preset) => preset.id)).toEqual([
      "oled",
      "accessibility",
      "compact",
      "performance",
    ]);
  });

  it("keeps reviewed snippet metadata and hot-path CSS constraints", () => {
    expect(SURFACE_SNIPPETS).toHaveLength(12);
    for (const snippet of SURFACE_SNIPPETS) {
      expect(snippet.source).toMatch(/^https:\/\//);
      expect(snippet.lastVerifiedSpotify).toBe("1.2.93");
      expect(snippet.css).not.toContain(":has(");
      expect(snippet.css).not.toContain("backdrop-filter");
    }
  });

  it("keeps mutually exclusive cover-art shapes from conflicting", () => {
    expect(
      updateSnippetSelection(
        ["hide-upgrade-button", "rounded-cover-art"],
        "circular-cover-art",
        true,
      ),
    ).toEqual(["hide-upgrade-button", "circular-cover-art"]);
    expect(
      updateSnippetSelection(["circular-cover-art"], "circular-cover-art", false),
    ).toEqual([]);
  });

  it("keeps focus, reduced motion, and responsive behavior in the surface CSS", () => {
    const css = readFileSync(resolve(import.meta.dirname, "../src/app.css"), "utf8");
    expect(css).toContain(":focus-visible");
    expect(css).toContain("@media (prefers-reduced-motion: reduce)");
    expect(css).toContain("@media (forced-colors: active)");
    expect(css).toContain("@media (max-width: 900px)");
    expect(css).toContain("@media (max-width: 780px)");
    expect(css).not.toContain(":has(");
    expect(css).not.toContain("backdrop-filter");
    expect(css).not.toContain("border-radius: 999px");
    expect(css).toContain(".librespot-snippet-preview");
    const layerCss = readFileSync(
      resolve(import.meta.dirname, "../src/core/layer-styles.ts"),
      "utf8",
    );
    expect(layerCss).toContain(".main-nowPlayingView-aboutArtist");
    expect(layerCss).toContain(".main-entityHeader-container");
    expect(layerCss).toContain(".main-actionBar-ActionBar");
    expect(layerCss).toContain(".Root__now-playing-bar .encore-internal-color-text-subdued");
    expect(layerCss).toContain('[role="alert"] > [data-encore-id="box"]');
  });

  it("keeps the large feature catalog searchable and grouped", () => {
    const source = readFileSync(
      resolve(import.meta.dirname, "../src/panels/features.ts"),
      "utf8",
    );
    const css = readFileSync(resolve(import.meta.dirname, "../src/app.css"), "utf8");
    expect(source).toContain('className: "librespot-feature-group"');
    expect(source).toContain('"aria-live": "polite"');
    expect(source).toContain('label: "Clear search"');
    expect(source).toContain("HTMLDetailsElement");
    expect(css).toContain(".librespot-feature-toolbar");
    expect(css).toContain(".librespot-feature-group[open]");
  });

  it("keeps the long Spicetify configuration catalog disclosed on demand", () => {
    const source = readFileSync(
      resolve(import.meta.dirname, "../src/panels/extensions.ts"),
      "utf8",
    );
    const css = readFileSync(resolve(import.meta.dirname, "../src/app.css"), "utf8");
    expect(source).toContain('"details"');
    expect(source).toContain("librespot-disclosure-section");
    expect(source).toContain("spicetifyOptions.length");
    expect(css).toContain(".librespot-disclosure-section[open]");
    expect(css).toContain(".librespot-disclosure-summary__copy");
  });

  it("counts only installed items represented by the managed catalogs", () => {
    const managedExtensionId = CUSTOMIZATION_CATALOG.extensions.at(0)?.id;
    expect(managedExtensionId).toBeDefined();
    if (!managedExtensionId) {
      throw new Error("The managed extension catalog must not be empty.");
    }

    expect(
      countInstalledManagedAssets(
        ["marketplace", "librespot"],
        CUSTOMIZATION_CATALOG.customApps,
      ),
    ).toBe(1);
    expect(
      countInstalledManagedAssets(
        ["unmanaged.js", managedExtensionId],
        CUSTOMIZATION_CATALOG.extensions,
      ),
    ).toBe(1);
  });

  it("keeps the source free of JSX and keyboard shortcut handlers", () => {
    const app = readFileSync(resolve(import.meta.dirname, "../src/app.ts"), "utf8");
    expect(app).not.toMatch(/return\s*\([^)]*<[A-Za-z]/s);
    expect(app).not.toContain("keydown");
    expect(app).not.toContain("keyup");
    expect(app).toContain("brandIconSource");
    expect(app).not.toContain('h("i")');
  });
});
