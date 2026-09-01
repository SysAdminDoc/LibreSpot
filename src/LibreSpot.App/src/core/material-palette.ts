import {
  argbFromHex,
  type DynamicColor,
  hexFromArgb,
  Hct,
  MaterialDynamicColors,
  SchemeContent,
  SchemeExpressive,
  SchemeFidelity,
  SchemeMonochrome,
  SchemeNeutral,
  SchemeTonalSpot,
  SchemeVibrant,
  type DynamicScheme,
} from "@material/material-color-utilities";
import { normalizeHex, type ColorScheme } from "./colors.ts";
import type { MaterialVariant } from "./state.ts";

type MaterialColorKey =
  | "onBackground"
  | "onSurfaceVariant"
  | "shadow"
  | "onSurface"
  | "primary"
  | "inversePrimary"
  | "outlineVariant"
  | "secondary"
  | "error"
  | "outline";

function createDynamicScheme(
  source: Hct,
  isDark: boolean,
  variant: MaterialVariant,
): DynamicScheme {
  switch (variant) {
    case "fidelity":
      return new SchemeFidelity(source, isDark, 0);
    case "vibrant":
      return new SchemeVibrant(source, isDark, 0);
    case "expressive":
      return new SchemeExpressive(source, isDark, 0);
    case "neutral":
      return new SchemeNeutral(source, isDark, 0);
    case "monochrome":
      return new SchemeMonochrome(source, isDark, 0);
    case "content":
      return new SchemeContent(source, isDark, 0);
    case "tonalSpot":
      return new SchemeTonalSpot(source, isDark, 0);
  }
}

function materialHex(
  scheme: DynamicScheme,
  key: MaterialColorKey,
): string {
  const dynamicColor: DynamicColor = MaterialDynamicColors[key];
  return normalizeHex(hexFromArgb(dynamicColor.getArgb(scheme)));
}

export function deriveMaterialSpiceScheme(
  sourceHex: string,
  isDark = true,
  variant: MaterialVariant = "tonalSpot",
): ColorScheme {
  const source = Hct.fromInt(argbFromHex(`#${normalizeHex(sourceHex)}`));
  const scheme = createDynamicScheme(source, isDark, variant);
  const palette = scheme.neutralVariantPalette;
  const surfaceTones = isDark ? [4, 10, 12, 17, 22] : [100, 96, 94, 92, 90];
  const surfaces = surfaceTones.map((tone) =>
    normalizeHex(hexFromArgb(palette.tone(tone))),
  );
  const surface = (index: number): string => {
    const value = surfaces[index];
    if (!value) {
      throw new Error(`Material surface tone ${index} was not generated.`);
    }
    return value;
  };

  return {
    text: materialHex(scheme, "onBackground"),
    subtext: materialHex(scheme, "onSurfaceVariant"),
    main: surface(0),
    "main-elevated": surface(1),
    highlight: surface(2),
    "highlight-elevated": surface(3),
    sidebar: surface(0),
    player: surface(1),
    card: surface(2),
    shadow: materialHex(scheme, "shadow"),
    "selected-row": materialHex(scheme, "onSurface"),
    button: materialHex(scheme, "primary"),
    "button-active": materialHex(scheme, "inversePrimary"),
    "button-disabled": materialHex(scheme, "outlineVariant"),
    "tab-active": surface(4),
    notification: materialHex(scheme, "secondary"),
    "notification-error": materialHex(scheme, "error"),
    misc: materialHex(scheme, "outline"),
    accent: materialHex(scheme, "primary"),
  };
}
