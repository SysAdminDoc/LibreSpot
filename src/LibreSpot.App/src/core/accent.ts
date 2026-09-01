import { normalizeHex, type ColorScheme } from "./colors.ts";
import { deriveMaterialSpiceScheme } from "./material-palette.ts";
import type { AccentPreset, EngineState } from "./state.ts";

export type ExtractedColors = Partial<
  Record<AccentPreset | "DARK_VIBRANT" | "DESATURATED", string>
>;

export type ColorExtractor = (
  imageOrUri: string,
) => Promise<ExtractedColors | null | undefined>;

export type AccentResult = {
  accent: string;
  scheme: ColorScheme | null;
  source: "album-art" | "fixed" | "os" | "scheme-fallback";
};

function candidateColor(colors: ExtractedColors, preset: AccentPreset): string | undefined {
  return colors[preset] ?? colors.VIBRANT ?? colors.LIGHT_VIBRANT ?? colors.PROMINENT;
}

export async function resolveAccent(
  state: EngineState,
  scheme: ColorScheme,
  options: {
    artworkUri?: string | undefined;
    extractor?: ColorExtractor | undefined;
    osAccent?: string | null | undefined;
    isDark?: boolean | undefined;
  } = {},
): Promise<AccentResult> {
  const fallback = normalizeHex(scheme.accent ?? scheme.button ?? "1ED760");
  let accent = fallback;
  let source: AccentResult["source"] = "scheme-fallback";

  if (state.dynamicAccent.mode === "fixed") {
    accent = normalizeHex(state.dynamicAccent.fixed);
    source = "fixed";
  } else if (state.dynamicAccent.mode === "os" && options.osAccent) {
    accent = normalizeHex(options.osAccent);
    source = "os";
  } else if (
    state.dynamicAccent.mode === "album-art" &&
    options.artworkUri &&
    options.extractor
  ) {
    try {
      const extracted = await options.extractor(options.artworkUri);
      const candidate = extracted
        ? candidateColor(extracted, state.dynamicAccent.preset)
        : undefined;
      if (candidate) {
        accent = normalizeHex(candidate);
        source = "album-art";
      }
    } catch {
      source = "scheme-fallback";
    }
  }

  return {
    accent,
    scheme: state.dynamicAccent.materialPalette
      ? deriveMaterialSpiceScheme(
          accent,
          options.isDark ?? true,
          state.dynamicAccent.materialVariant,
        )
      : null,
    source,
  };
}
