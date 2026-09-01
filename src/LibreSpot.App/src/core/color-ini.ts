import {
  deriveScheme,
  normalizeHex,
  SPICE_COLOR_KEYS,
  type ColorScheme,
} from "./colors.ts";

export type ColorIniDocument = {
  sectionOrder: string[];
  schemes: Record<string, ColorScheme>;
};

export class ColorIniParseError extends Error {
  public constructor(
    message: string,
    public readonly lineNumber: number,
  ) {
    super(`color.ini line ${lineNumber}: ${message}`);
    this.name = "ColorIniParseError";
  }
}

const SECTION_PATTERN = /^\s*\[([^\]]+)]\s*(?:[;#].*)?$/;
const ENTRY_PATTERN = /^\s*([A-Za-z0-9_-]+)\s*=\s*([^;#\s]+)\s*(?:[;#].*)?$/;

export function parseColorIni(source: string): ColorIniDocument {
  const schemes: Record<string, ColorScheme> = {};
  const sectionOrder: string[] = [];
  let currentSection: string | undefined;

  for (const [index, rawLine] of source.replace(/^\uFEFF/, "").split(/\r?\n/).entries()) {
    const lineNumber = index + 1;
    const line = rawLine.trim();
    if (line === "" || line.startsWith(";") || line.startsWith("#")) {
      continue;
    }

    const sectionMatch = SECTION_PATTERN.exec(rawLine);
    if (sectionMatch) {
      const section = sectionMatch[1]?.trim();
      if (!section) {
        throw new ColorIniParseError("Section name is empty.", lineNumber);
      }
      if (schemes[section]) {
        throw new ColorIniParseError(`Duplicate section "${section}".`, lineNumber);
      }
      currentSection = section;
      schemes[section] = {};
      sectionOrder.push(section);
      continue;
    }

    const entryMatch = ENTRY_PATTERN.exec(rawLine);
    if (!entryMatch) {
      throw new ColorIniParseError("Expected a section or key=value entry.", lineNumber);
    }
    if (!currentSection) {
      throw new ColorIniParseError("Color entry appears before the first section.", lineNumber);
    }

    const keyToken = entryMatch[1];
    const valueToken = entryMatch[2];
    const currentScheme = schemes[currentSection];
    if (!keyToken || !valueToken || !currentScheme) {
      throw new ColorIniParseError("Color entry could not be read.", lineNumber);
    }
    const key = keyToken.toLowerCase();
    if (currentScheme[key]) {
      throw new ColorIniParseError(
        `Duplicate color key "${key}" in section "${currentSection}".`,
        lineNumber,
      );
    }
    try {
      currentScheme[key] = normalizeHex(valueToken);
    } catch (error) {
      const message = error instanceof Error ? error.message : "Invalid color value.";
      throw new ColorIniParseError(message, lineNumber);
    }
  }

  if (sectionOrder.length === 0) {
    throw new ColorIniParseError("No color sections were found.", 1);
  }
  return { sectionOrder, schemes };
}

export function serializeColorIni(
  document: ColorIniDocument,
  options: { deriveMissing?: boolean } = {},
): string {
  const sections = document.sectionOrder.map((section) => {
    const rawScheme = document.schemes[section];
    if (!rawScheme) {
      throw new Error(`Missing color data for section "${section}".`);
    }
    const scheme = options.deriveMissing ? deriveScheme(rawScheme) : rawScheme;
    const orderedKeys = [
      ...SPICE_COLOR_KEYS.filter((key) => key in scheme),
      ...Object.keys(scheme)
        .filter(
          (key) => !SPICE_COLOR_KEYS.some((defaultKey) => defaultKey === key),
        )
        .sort((left, right) => left.localeCompare(right)),
    ];
    const width = Math.max(...orderedKeys.map((key) => key.length), 1);
    const entries = orderedKeys.map((key) => {
      const value = scheme[key];
      if (!value) {
        throw new Error(`Missing value for color "${key}".`);
      }
      return `${key.padEnd(width)} = ${normalizeHex(value)}`;
    });
    return `[${section}]\n${entries.join("\n")}`;
  });
  return `${sections.join("\n\n")}\n`;
}

export function getDerivedScheme(
  document: ColorIniDocument,
  section: string,
): ColorScheme {
  const scheme = document.schemes[section];
  if (!scheme) {
    throw new Error(`Unknown color scheme "${section}".`);
  }
  return deriveScheme(scheme);
}
