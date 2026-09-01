import {
  CUSTOMIZATION_CATALOG,
  FeatureCapture,
  applyUserPreset,
  captureUserPreset,
  createDefaultState,
} from "../src/core/index.ts";

describe("shared customization catalog", () => {
  it("contains every unique flag extracted from the pinned bundle", () => {
    const features = CUSTOMIZATION_CATALOG.spotifyFeatures;
    expect(features).toHaveLength(348);
    expect(new Set(features.map((feature) => feature.name)).size).toBe(348);
    expect(features.every((feature) => feature.description.length > 0)).toBe(true);
    expect(features.every((feature) => CUSTOMIZATION_CATALOG.featureGroups.includes(feature.group))).toBe(true);
    expect(CUSTOMIZATION_CATALOG.pins.xpuiSha256).toMatch(/^[a-f\d]{64}$/);
  });

  it("records active SpotX values without dropping undeclared patch entries", () => {
    expect(CUSTOMIZATION_CATALOG.spotxFeatureOverrides).toHaveLength(104);
    expect(
      CUSTOMIZATION_CATALOG.spotxFeatureOverrides.some(
        (override) => override.name === "enableInAppMessaging" && override.value === false,
      ),
    ).toBe(true);
    expect(
      CUSTOMIZATION_CATALOG.spotifyFeatures.some((feature) => feature.spotxForced),
    ).toBe(true);
  });

  it("surfaces the pinned SpotX and Spicetify configuration contracts", () => {
    expect(CUSTOMIZATION_CATALOG.spotxSwitches).toHaveLength(31);
    expect(CUSTOMIZATION_CATALOG.spicetifyOptions).toHaveLength(21);
    expect(
      CUSTOMIZATION_CATALOG.spicetifyOptions.map((option) => option.id),
    ).toContain("experimental_features");
    expect(
      CUSTOMIZATION_CATALOG.spotxSwitches.map((control) => control.configKey),
    ).toContain("SpotX_CustomPatchesEnabled");
  });

  it("normalizes Spotify enum objects captured at runtime", () => {
    const capture = new FeatureCapture();
    const feature = capture.capture({
      name: "variant",
      description: "Variant",
      type: "enum",
      default: "CONTROL",
      values: { CONTROL: "CONTROL", TEST: "TEST" } as unknown as string[],
    });
    expect(feature.values).toEqual(["CONTROL", "TEST"]);
  });

  it("captures and reapplies user presets without recursive preset state", () => {
    const state = createDefaultState(new Date("2026-09-01T12:00:00Z"));
    state.schemes = { Dark: { main: "000000", text: "FFFFFF" } };
    state.theme = "Compact";
    state.enabledSnippets = ["compact-track-rows"];
    const preset = captureUserPreset(state, "user-one", "Desk");
    state.theme = "Prism";
    state.enabledSnippets = [];
    applyUserPreset(state, preset);
    expect(state.theme).toBe("Compact");
    expect(state.enabledSnippets).toEqual(["compact-track-rows"]);
    expect("userPresets" in preset).toBe(false);
  });
});
