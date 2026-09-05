import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  CUSTOMIZATION_CATALOG,
  contrastRatio,
  readableText,
  validateSchemeContrast,
} from "../src/core/index.ts";
import {
  countInstalledManagedAssets,
  STORE_THEMES,
  themeDescription,
} from "../src/panels/store.ts";
import {
  countCustomizedFeatures,
  isCustomizedFeature,
  withFeatureReverted,
} from "../src/panels/features.ts";
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
  it("makes the Store the first of six focused panels", () => {
    expect(PANEL_DEFINITIONS.map((panel) => panel.id)).toEqual([
      "store",
      "look",
      "tweaks",
      "features",
      "presets",
      "health",
    ]);
    for (const panel of PANEL_DEFINITIONS) {
      expect(panelFromPath(panelPath(panel.id))).toBe(panel.id);
    }
    expect(panelFromPath("/librespot")).toBe("store");
    expect(panelFromPath("/librespot/extensions")).toBe("store");
    expect(panelFromPath("/librespot/marketplace")).toBe("store");
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
    expect(css).toContain("@media (max-width: 1200px)");
    expect(css).toContain("@container librespot-content (max-width: 820px)");
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

  it("marks only changed flags and reverts one without touching the rest", () => {
    const overrides = { automix_enabled: true, enableEqualizer: false } as const;

    expect(isCustomizedFeature(overrides, "automix_enabled")).toBe(true);
    expect(isCustomizedFeature(overrides, "enableSleepTimer")).toBe(false);
    expect(
      countCustomizedFeatures(overrides, [
        "automix_enabled",
        "enableEqualizer",
        "enableSleepTimer",
      ]),
    ).toBe(2);

    // Reverting one flag drops only that key. The engine restores Spotify's own
    // value for whatever disappeared, so the count has to fall by exactly one.
    const reverted = withFeatureReverted(overrides, "automix_enabled");
    expect(reverted).toEqual({ enableEqualizer: false });
    expect(isCustomizedFeature(reverted, "automix_enabled")).toBe(false);
    expect(isCustomizedFeature(reverted, "enableEqualizer")).toBe(true);
    expect(countCustomizedFeatures(reverted, ["automix_enabled", "enableEqualizer"])).toBe(1);

    // A flag that was never customized leaves the map untouched.
    expect(withFeatureReverted(reverted, "enableSleepTimer")).toEqual(reverted);
    // The original is not mutated.
    expect(Object.keys(overrides).sort()).toEqual(["automix_enabled", "enableEqualizer"]);
  });

  it("gives every changed flag a revert control with an accessible name", () => {
    const source = readFileSync(
      resolve(import.meta.dirname, "../src/panels/features.ts"),
      "utf8",
    );

    expect(source).toContain('label: "Revert"');
    expect(source).toContain("accessibleLabel: `Revert ${feature.name} to Spotify's value`");
    // The control is only built for a customized flag, and every row type gets it.
    expect(source).toContain("const action = isCustom");
    expect(source.match(/\.\.\.\(action \? \{ action \} : \{\}\)/g)?.length).toBe(4);
  });

  it("keeps the large feature catalog searchable and browsable by group", () => {
    const source = readFileSync(
      resolve(import.meta.dirname, "../src/panels/features.ts"),
      "utf8",
    );
    const css = readFileSync(resolve(import.meta.dirname, "../src/app.css"), "utf8");
    expect(source).toContain('className: "librespot-feature-group"');
    expect(source).toContain('"aria-live": "polite"');
    expect(source).toContain('label: "Clear search"');
    expect(source).toContain('className: "librespot-feature-workspace"');
    expect(source).toContain('"aria-label": "Feature groups"');
    expect(source).toContain('"aria-label": "Feature source"');
    expect(css).toContain(".librespot-feature-toolbar");
    expect(css).toContain(".librespot-feature-workspace");
    expect(css).toContain(".librespot-feature-groups button.is-active");
  });

  it("keeps the long Spicetify configuration catalog disclosed on demand", () => {
    const source = readFileSync(
      resolve(import.meta.dirname, "../src/panels/store.ts"),
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

  it("shows every supported theme once with useful copy and a real preview", () => {
    expect(STORE_THEMES).toHaveLength(24);
    expect(new Set(STORE_THEMES.map((theme) => theme.id)).size).toBe(STORE_THEMES.length);
    expect(STORE_THEMES.some((theme) => theme.marketplaceOnly)).toBe(false);
    for (const theme of STORE_THEMES) {
      expect(themeDescription(theme).length).toBeGreaterThan(24);
      expect(theme.preview?.status).toBe("available");
      expect(theme.preview?.url).toBeTruthy();
    }
  });

  it("keeps the source free of JSX and keyboard shortcut handlers", () => {
    const app = readFileSync(resolve(import.meta.dirname, "../src/app.ts"), "utf8");
    expect(app).not.toMatch(/return\s*\([^)]*<[A-Za-z]/s);
    expect(app).not.toContain("keydown");
    expect(app).not.toContain("keyup");
    expect(app).toContain("brandIconSource");
    expect(app).not.toContain('h("i")');
  });

  it("uses stable keys for live arrangement rows", () => {
    const tweaks = readFileSync(
      resolve(import.meta.dirname, "../src/panels/tweaks.ts"),
      "utf8",
    );
    expect(tweaks).toContain("{ key: item.id }");
    expect(tweaks).not.toContain("{ key: item },");
  });

  it("keeps Store in the profile menu and opens Look from the main settings button", () => {
    const extension = readFileSync(
      resolve(import.meta.dirname, "../src/extensions/librespot-engine.ts"),
      "utf8",
    );
    expect(extension).toContain('new MenuItem("LibreSpot Store"');
    expect(extension).toContain('new TopbarButton("LibreSpot Settings"');
    expect(extension).toContain('Spicetify.Platform.History.push(panelPath("look"))');
    expect(extension).toContain('lucide-static/icons/settings.svg');
  });

  it("lets duplicate companion loads converge on one ready runtime", () => {
    const extension = readFileSync(
      resolve(import.meta.dirname, "../src/extensions/librespot-engine.ts"),
      "utf8",
    );
    expect(extension).not.toContain("if (window.__libreSpotEngineLoaded)");
    expect(extension.match(/if \(runtimeIsReady\(\)\)/g)).toHaveLength(2);
    expect(extension.indexOf("await waitForApi()"))
      .toBeLessThan(extension.lastIndexOf("if (runtimeIsReady())"));
    expect(extension.indexOf("await engine.start"))
      .toBeLessThan(extension.indexOf("window.__libreSpotEngineLoaded = true"));
  });
});
