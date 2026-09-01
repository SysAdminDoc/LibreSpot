export const SPICE_COLOR_DEFAULTS = {
  text: "FFFFFF",
  subtext: "B3B3B3",
  main: "121212",
  "main-elevated": "181818",
  highlight: "1A1A1A",
  "highlight-elevated": "242424",
  sidebar: "000000",
  player: "181818",
  card: "181818",
  shadow: "000000",
  "selected-row": "FFFFFF",
  button: "1ED760",
  "button-active": "1FDF64",
  "button-disabled": "535353",
  "tab-active": "333333",
  notification: "4687D6",
  "notification-error": "E22134",
  misc: "7F7F7F",
  accent: "1ED760",
} as const;

export type SpiceColorKey = keyof typeof SPICE_COLOR_DEFAULTS;
export type ColorScheme = Record<string, string>;

export const SPICE_COLOR_KEYS = Object.freeze(
  Object.keys(SPICE_COLOR_DEFAULTS) as SpiceColorKey[],
);

export function normalizeHex(input: string): string {
  const value = input.trim().replace(/^#/, "");
  if (/^[0-9a-f]{3}$/i.test(value)) {
    return value
      .split("")
      .map((part) => part + part)
      .join("")
      .toUpperCase();
  }
  if (!/^[0-9a-f]{6}$/i.test(value)) {
    throw new Error(`Expected a 3 or 6 digit hex color, received "${input}".`);
  }
  return value.toUpperCase();
}

export function hexToRgb(input: string): readonly [number, number, number] {
  const value = normalizeHex(input);
  return [
    Number.parseInt(value.slice(0, 2), 16),
    Number.parseInt(value.slice(2, 4), 16),
    Number.parseInt(value.slice(4, 6), 16),
  ];
}

export function rgbCss(input: string): string {
  return hexToRgb(input).join(", ");
}

function linearChannel(channel: number): number {
  const value = channel / 255;
  return value <= 0.04045
    ? value / 12.92
    : ((value + 0.055) / 1.055) ** 2.4;
}

export function relativeLuminance(input: string): number {
  const [red, green, blue] = hexToRgb(input);
  return (
    0.2126 * linearChannel(red) +
    0.7152 * linearChannel(green) +
    0.0722 * linearChannel(blue)
  );
}

export function contrastRatio(first: string, second: string): number {
  const light = Math.max(relativeLuminance(first), relativeLuminance(second));
  const dark = Math.min(relativeLuminance(first), relativeLuminance(second));
  return (light + 0.05) / (dark + 0.05);
}

export function readableText(background: string): "000000" | "FFFFFF" {
  const white = contrastRatio(background, "FFFFFF");
  const black = contrastRatio(background, "000000");
  return white >= black ? "FFFFFF" : "000000";
}

export function deriveScheme(input: ColorScheme): ColorScheme {
  const normalized: ColorScheme = {};
  for (const [key, value] of Object.entries(input)) {
    normalized[key.toLowerCase()] = normalizeHex(value);
  }

  const derived: ColorScheme = {};
  for (const key of SPICE_COLOR_KEYS) {
    derived[key] = normalized[key] ?? SPICE_COLOR_DEFAULTS[key];
  }
  for (const [key, value] of Object.entries(normalized)) {
    if (!(key in derived)) {
      derived[key] = value;
    }
  }
  return derived;
}

export function validateSchemeContrast(
  scheme: ColorScheme,
  minimum = 4.5,
): readonly string[] {
  const complete = deriveScheme(scheme);
  const color = (key: SpiceColorKey): string =>
    complete[key] ?? SPICE_COLOR_DEFAULTS[key];
  const pairs: readonly (readonly [string, string, string])[] = [
    ["text/main", color("text"), color("main")],
    ["subtext/main", color("subtext"), color("main")],
    ["text/player", color("text"), color("player")],
    ["text/sidebar", color("text"), color("sidebar")],
    ["main/button", color("main"), color("button")],
  ];
  return pairs
    .filter(([, foreground, background]) =>
      contrastRatio(foreground, background) < minimum,
    )
    .map(([name, foreground, background]) =>
      `${name} is ${contrastRatio(foreground, background).toFixed(2)}:1`,
    );
}
