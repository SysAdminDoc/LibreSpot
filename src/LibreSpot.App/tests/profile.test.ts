import { describe, expect, it } from "vitest";
import { Script } from "node:vm";
import {
  createDefaultState,
  deriveScheme,
  exportTheme,
  importColorIniIntoState,
  MAX_PROFILE_BYTES,
  parseColorIni,
  parseProfile,
  serializeProfile,
} from "../src/core/index.ts";

function stateFixture() {
  const state = createDefaultState(new Date("2026-09-01T12:00:00Z"));
  state.name = "Night desk";
  state.schemes = {
    Dark: {
      text: "FFFFFF",
      main: "000000",
      button: "1ED760",
      accent: "1ED760",
    },
    Light: {
      text: "111111",
      main: "FFFFFF",
      button: "16843D",
      accent: "16843D",
    },
  };
  return state;
}

describe("profile and theme export", () => {
  it("round-trips the profile state", () => {
    const state = stateFixture();
    expect(parseProfile(serializeProfile(state))).toEqual(state);
  });

  it("exports a standard theme folder with complete schemes", () => {
    const state = stateFixture();
    const exported = exportTheme(state);
    const parsedExport = parseColorIni(exported["color.ini"]);

    for (const [name, input] of Object.entries(state.schemes)) {
      expect(parsedExport.schemes[name]).toEqual(deriveScheme(input));
    }
    expect(exported["user.css"]).toContain("librespot-tier-glass");
    expect(exported["theme.js"]).toContain("librespot-layer-palette");
  });

  it("imports color.ini into engine state without a second serialization pass", () => {
    const input = `[One]\ntext = FFFFFF\nmain = 000000\n\n[Two]\ntext = 000000\nmain = FFFFFF\n`;
    const imported = importColorIniIntoState(input);
    expect(imported.scheme).toBe("One");
    expect(imported.schemes).toEqual(parseColorIni(input).schemes);
  });

  it("rejects profiles that name a missing scheme", () => {
    const state = stateFixture();
    state.scheme = "Missing";
    expect(() => parseProfile(serializeProfile(state))).toThrow(/references missing scheme/);
  });

  it("rejects malformed nested state instead of filling it with defaults", () => {
    const state = stateFixture();
    const malformed = JSON.parse(serializeProfile(state)) as {
      settings: { LibreSpot_EngineProfileJson: string };
    };
    const nested = JSON.parse(malformed.settings.LibreSpot_EngineProfileJson) as Record<string, unknown>;
    nested.layers = { palette: "yes" };
    malformed.settings.LibreSpot_EngineProfileJson = JSON.stringify(nested);

    expect(() => parseProfile(JSON.stringify(malformed))).toThrow(/layers\.palette/);
  });

  it("rejects oversized profile input before parsing", () => {
    expect(() => parseProfile("x".repeat(MAX_PROFILE_BYTES + 1))).toThrow(/byte limit/);
  });

  it("serializes numeric-looking theme values as inert data", () => {
    const state = stateFixture();
    (state.appearance as unknown as { radius: string }).radius =
      '12px"); window.__libreSpotInjected = true; /*';
    const exported = exportTheme(state)["theme.js"];

    expect(exported).toContain('setProperty("--librespot-radius",');
    expect(() => {
      new Script(exported).runInNewContext({
        document: {
          documentElement: {
            classList: { add: (): void => undefined },
            style: { setProperty: (): void => undefined },
          },
        },
      });
    }).not.toThrow();
  });
});
