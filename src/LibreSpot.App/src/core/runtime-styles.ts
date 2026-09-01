import { deriveScheme, normalizeHex, rgbCss, type ColorScheme } from "./colors.ts";
import {
  LAYER_CSS,
  LAYER_STYLE_ID,
  PALETTE_STYLE_ID,
  SNIPPET_STYLE_ID,
  THEME_STYLE_ID,
} from "./layer-styles.ts";
import type { EffectsTier, EngineState, LayerState } from "./state.ts";

const LAYER_CLASSES = [
  "librespot-layer-palette",
  "librespot-layer-layout",
  "librespot-layer-effects",
  "librespot-layer-accessibility",
] as const;
const TIER_CLASSES = [
  "librespot-tier-glass",
  "librespot-tier-eco",
  "librespot-tier-flat",
] as const;

function cssValue(value: string): string {
  return value.replace(/[\n\r;{}]/g, "");
}

export class ManagedRuntimeStyles {
  public constructor(private readonly document: Document) {}

  public installLayerStyles(): HTMLStyleElement {
    const style = this.ensureStyle(LAYER_STYLE_ID);
    style.textContent = LAYER_CSS;
    return style;
  }

  public applyPalette(scheme: ColorScheme, enabled = true): HTMLStyleElement {
    const style = this.ensureStyle(PALETTE_STYLE_ID);
    if (!enabled) {
      style.textContent = "";
      return style;
    }
    const variables = Object.entries(deriveScheme(scheme)).flatMap(([key, value]) => {
      const color = normalizeHex(value);
      return [
        `--spice-${key}: #${color};`,
        `--spice-rgb-${key}: ${rgbCss(color)};`,
      ];
    });
    // Spicetify can append color.css and user.css after extensions start. The
    // layer class gives this managed rule enough specificity to keep a live
    // scheme change visible even when those later :root rules load afterward.
    style.textContent = `:root.librespot-layer-palette {\n  ${variables.join("\n  ")}\n}`;
    return style;
  }

  public applyLayers(layers: LayerState, tier: EffectsTier): void {
    const root = this.document.documentElement;
    root.classList.remove(...LAYER_CLASSES, ...TIER_CLASSES);
    for (const [name, enabled] of Object.entries(layers)) {
      if (enabled) {
        root.classList.add(`librespot-layer-${name}`);
      }
    }
    root.classList.add(`librespot-tier-${tier}`);
    root.dataset.librespotEffectsTier = tier;
  }

  public applyAppearance(state: EngineState): void {
    const root = this.document.documentElement;
    root.style.setProperty("--librespot-font", cssValue(state.appearance.fontFamily));
    root.style.setProperty("--librespot-radius", `${state.appearance.radius}px`);
    for (const [region, scale] of Object.entries(state.appearance.scale)) {
      root.style.setProperty(`--librespot-scale-${region}`, String(scale));
    }
  }

  public applyTheme(className: string, css: string): HTMLStyleElement {
    const root = this.document.documentElement;
    for (const name of [...root.classList]) {
      if (name.startsWith("librespot-theme-")) root.classList.remove(name);
    }
    if (className) root.classList.add(className);
    const style = this.ensureStyle(THEME_STYLE_ID);
    style.textContent = css;
    return style;
  }

  public applySnippets(cssBlocks: readonly string[]): HTMLStyleElement {
    const style = this.ensureStyle(SNIPPET_STYLE_ID);
    style.textContent = cssBlocks.join("\n\n");
    return style;
  }

  public setReducedMotion(enabled: boolean): void {
    this.document.documentElement.classList.toggle(
      "librespot-reduced-motion",
      enabled,
    );
  }

  public setHighContrast(enabled: boolean): void {
    this.document.documentElement.classList.toggle(
      "librespot-high-contrast",
      enabled,
    );
  }

  public setAccent(color: string | null): void {
    if (color) {
      this.document.documentElement.style.setProperty(
        "--librespot-accent",
        `#${normalizeHex(color)}`,
      );
    } else {
      this.document.documentElement.style.removeProperty("--librespot-accent");
    }
  }

  public dispose(): void {
    for (const id of [LAYER_STYLE_ID, PALETTE_STYLE_ID, SNIPPET_STYLE_ID, THEME_STYLE_ID]) {
      this.document.getElementById(id)?.remove();
    }
    const root = this.document.documentElement;
    root.classList.remove(
      ...LAYER_CLASSES,
      ...TIER_CLASSES,
      "librespot-reduced-motion",
      "librespot-high-contrast",
    );
    for (const name of [...root.classList]) {
      if (name.startsWith("librespot-theme-")) root.classList.remove(name);
    }
    root.removeAttribute("data-librespot-effects-tier");
    const properties = Array.from(
      { length: root.style.length },
      (_, index) => root.style.item(index),
    );
    for (const property of properties) {
      if (property.startsWith("--librespot-")) {
        root.style.removeProperty(property);
      }
    }
  }

  private ensureStyle(id: string): HTMLStyleElement {
    const existing = this.document.getElementById(id);
    if (existing instanceof HTMLStyleElement) {
      return existing;
    }
    const style = this.document.createElement("style");
    style.id = id;
    style.dataset.librespotManaged = "true";
    this.document.head.append(style);
    return style;
  }
}
