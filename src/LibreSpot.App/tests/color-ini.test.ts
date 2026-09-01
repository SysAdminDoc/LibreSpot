import {
  ColorIniParseError,
  deriveScheme,
  getDerivedScheme,
  parseColorIni,
  serializeColorIni,
  SPICE_COLOR_KEYS,
} from "../src/core/index.ts";

const canonical = `[Dark]
text   = FFFFFF
main   = 121212
button = 1ED760
accent = 1ED760

[Light]
text   = 111111
main   = FFFFFF
button = 16843D
accent = 16843D
`;

describe("color.ini", () => {
  it("round-trips a canonical input directly", () => {
    expect(serializeColorIni(parseColorIni(canonical))).toBe(canonical);
  });

  it("derives every required Spicetify key while keeping extras", () => {
    const scheme = deriveScheme({ main: "#000", brand: "abc" });
    expect(Object.keys(scheme)).toEqual(
      expect.arrayContaining([...SPICE_COLOR_KEYS, "brand"]),
    );
    expect(scheme.main).toBe("000000");
    expect(scheme.brand).toBe("AABBCC");
  });

  it("selects and completes a named section", () => {
    const document = parseColorIni(canonical);
    const scheme = getDerivedScheme(document, "Light");
    expect(scheme.main).toBe("FFFFFF");
    expect(scheme.player).toBeDefined();
  });

  it("reports the source line for malformed colors", () => {
    expect(() => parseColorIni("[Dark]\nmain = purple\n")).toThrow(
      new ColorIniParseError(
        'Expected a 3 or 6 digit hex color, received "purple".',
        2,
      ),
    );
  });

  it("rejects duplicate sections and keys", () => {
    expect(() => parseColorIni("[Dark]\nmain=000\nmain=fff\n")).toThrow(
      /Duplicate color key/,
    );
    expect(() => parseColorIni("[Dark]\nmain=000\n[Dark]\n")).toThrow(
      /Duplicate section/,
    );
  });
});
