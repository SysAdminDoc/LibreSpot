import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  contrastRatio,
  readableText,
  validateSchemeContrast,
} from "../src/core/index.ts";
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

  it("keeps focus, reduced motion, and responsive behavior in the surface CSS", () => {
    const css = readFileSync(resolve(import.meta.dirname, "../src/app.css"), "utf8");
    expect(css).toContain(":focus-visible");
    expect(css).toContain("@media (prefers-reduced-motion: reduce)");
    expect(css).toContain("@media (max-width: 900px)");
    expect(css).toContain("@media (max-width: 700px)");
    expect(css).not.toContain(":has(");
    expect(css).not.toContain("backdrop-filter");
    expect(css).toContain(".librespot-snippet-preview");
  });

  it("keeps the source free of JSX and keyboard shortcut handlers", () => {
    const app = readFileSync(resolve(import.meta.dirname, "../src/app.ts"), "utf8");
    expect(app).not.toMatch(/return\s*\([^)]*<[A-Za-z]/s);
    expect(app).not.toContain("keydown");
    expect(app).not.toContain("keyup");
  });
});
