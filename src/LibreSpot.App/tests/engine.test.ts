import {
  createDefaultState,
  EngineStore,
  LibreSpotEngine,
  type StorageAdapter,
} from "../src/core/index.ts";

function memoryStorage(): StorageAdapter {
  const values = new Map<string, string>();
  return {
    get: (key) => values.get(key) ?? null,
    set: (key, value) => {
      values.set(key, value);
    },
    remove: (key) => {
      values.delete(key);
    },
  };
}

describe("LibreSpot engine", () => {
  beforeEach(() => {
    document.head.innerHTML = "";
    document.body.innerHTML = "";
    document.documentElement.className = "";
    document.documentElement.removeAttribute("style");
  });

  it("applies saved state and every common change without a reload", async () => {
    const state = createDefaultState(new Date("2026-09-01T12:00:00Z"));
    state.schemes = {
      Dark: { main: "000000", text: "FFFFFF", accent: "1ED760" },
      Light: { main: "FFFFFF", text: "111111", accent: "16843D" },
    };
    const store = new EngineStore(memoryStorage(), () =>
      new Date("2026-09-01T12:00:00Z"),
    );
    const engine = new LibreSpotEngine({
      document,
      window,
      store,
      initialState: state,
      snippetCss: { compact: ".Root__main-view { --row-height: 32px; }" },
    });
    await engine.start({ probePerformance: false });

    const originalDocument = document;
    engine.update((draft) => {
      draft.scheme = "Light";
      draft.effectsTier = "flat";
      draft.layers.effects = false;
      draft.enabledSnippets = ["compact"];
    });

    expect(document).toBe(originalDocument);
    expect(engine.activeScheme).toBe("Light");
    expect(document.documentElement.classList.contains("librespot-tier-flat")).toBe(
      true,
    );
    expect(
      document.documentElement.classList.contains("librespot-layer-effects"),
    ).toBe(false);
    expect(document.getElementById("librespot-engine-snippets")?.textContent).toContain(
      "--row-height",
    );
    expect(document.getElementById("librespot-engine-palette")?.textContent).toContain(
      "--spice-main: #FFFFFF",
    );
  });

  it("applies a preview and restores the saved scheme", async () => {
    const state = createDefaultState();
    state.schemes = {
      Dark: { main: "000000", text: "FFFFFF" },
    };
    const engine = new LibreSpotEngine({
      document,
      window,
      store: new EngineStore(memoryStorage()),
      initialState: state,
    });
    await engine.start({ probePerformance: false });
    engine.applyPreviewScheme({ main: "123456", text: "FFFFFF" });
    expect(document.getElementById("librespot-engine-palette")?.textContent).toContain(
      "#123456",
    );
    engine.clearPreview();
    expect(document.getElementById("librespot-engine-palette")?.textContent).toContain(
      "#000000",
    );
  });
});
