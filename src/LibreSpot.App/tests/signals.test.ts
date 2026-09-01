import {
  applyFeatureOverrides,
  classifyFrameRate,
  createDefaultState,
  deriveMaterialSpiceScheme,
  isLightScheduleActive,
  resolveAccent,
  resolveScheduledScheme,
  type RemoteProperty,
} from "../src/core/index.ts";

describe("runtime signals", () => {
  it("handles day schedules that cross midnight", () => {
    expect(
      isLightScheduleActive(new Date(2026, 8, 1, 23, 0), "22:00", "06:00"),
    ).toBe(true);
    expect(
      isLightScheduleActive(new Date(2026, 8, 1, 12, 0), "22:00", "06:00"),
    ).toBe(false);
  });

  it("resolves scheduled schemes without changing the saved choice", () => {
    const state = createDefaultState();
    state.scheme = "OLED";
    state.schedule.enabled = true;
    state.schedule.lightScheme = "Light";
    state.schedule.darkScheme = "Dark";
    expect(resolveScheduledScheme(state, new Date(2026, 8, 1, 8, 0))).toBe(
      "Light",
    );
    expect(state.scheme).toBe("OLED");
  });

  it("classifies frame rates into glass, eco, and flat", () => {
    expect(classifyFrameRate(60)).toBe("glass");
    expect(classifyFrameRate(44)).toBe("eco");
    expect(classifyFrameRate(20)).toBe("flat");
  });

  it("uses artwork accent and can derive a full Material palette", async () => {
    const state = createDefaultState();
    state.dynamicAccent.materialPalette = true;
    const result = await resolveAccent(
      state,
      { accent: "1ED760", main: "121212" },
      {
        artworkUri: "spotify:image:test",
        extractor: () => Promise.resolve({ VIBRANT: "#336699" }),
      },
    );
    expect(result).toEqual(
      expect.objectContaining({ accent: "336699", source: "album-art" }),
    );
    expect(result.scheme?.accent).toMatch(/^[A-F0-9]{6}$/);
  });

  it("builds an AA-aware Material scheme from a seed", () => {
    const palette = deriveMaterialSpiceScheme("1ED760", true, "vibrant");
    expect(palette.main).toMatch(/^[A-F0-9]{6}$/);
    expect(palette.text).toMatch(/^[A-F0-9]{6}$/);
    expect(palette.accent).toBe(palette.button);
  });

  it("applies matching remote properties through the debug API", async () => {
    const properties: RemoteProperty[] = [
      {
        source: "web",
        type: "boolean",
        name: "enableExample",
        localValue: false,
      },
    ];
    const setOverrides = vi.fn(() => Promise.resolve(undefined));
    const result = await applyFeatureOverrides(
      { enableExample: true, missing: false },
      {
        debugApi: {
          getProperties: () => Promise.resolve(properties),
          setOverrides,
        },
      },
    );
    expect(result).toBe("debug-api");
    expect(setOverrides).toHaveBeenCalledWith(
      [{ ref: properties[0], value: true }],
      { autoRunOverrideEffects: true },
    );
  });
});
