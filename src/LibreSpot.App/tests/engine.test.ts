import {
  createDefaultState,
  ENGINE_STORAGE_KEY,
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

  it("previews a bundled theme without replacing the saved choice", async () => {
    const state = createDefaultState();
    state.theme = "Prism";
    state.schemes = {
      Dark: { main: "000000", text: "FFFFFF" },
      HighContrast: { main: "010101", text: "FFFFFF" },
    };
    const engine = new LibreSpotEngine({
      document,
      window,
      store: new EngineStore(memoryStorage()),
      initialState: state,
      themeStyles: {
        Prism: { className: "librespot-theme-prism", css: ".prism { color: white; }" },
        Accessibility: { className: "librespot-theme-accessibility", css: ".accessible { outline: 2px solid; }" },
      },
    });
    await engine.start({ probePerformance: false });

    expect(engine.applyPreviewTheme("Accessibility", "HighContrast")).toBe(true);
    expect(document.documentElement.classList).toContain("librespot-theme-accessibility");
    expect(document.getElementById("librespot-engine-theme")?.textContent).toContain("outline");
    expect(document.getElementById("librespot-engine-palette")?.textContent).toContain("#010101");
    expect(engine.state.theme).toBe("Prism");
    expect(engine.applyPreviewTheme("Unknown")).toBe(false);

    engine.clearPreview();
    expect(document.documentElement.classList).toContain("librespot-theme-prism");
    expect(document.getElementById("librespot-engine-theme")?.textContent).toContain(".prism");
  });

  it("keeps the saved state and active appearance when validation rejects an edit", async () => {
    const state = createDefaultState();
    state.schemes = {
      Dark: { main: "000000", text: "FFFFFF" },
      Light: { main: "FFFFFF", text: "111111" },
    };
    const values = new Map<string, string>();
    const storage: StorageAdapter = {
      get: (key) => values.get(key) ?? null,
      set: (key, value) => values.set(key, value),
      remove: (key) => values.delete(key),
    };
    const store = new EngineStore(storage);
    const persisted = store.save(state);
    const engine = new LibreSpotEngine({
      document,
      window,
      store,
      initialState: persisted,
    });
    await engine.start({ probePerformance: false });
    const beforeState = engine.state;
    const beforeProfile = values.get(ENGINE_STORAGE_KEY);

    expect(() => {
      engine.update((draft) => {
        draft.scheme = "Missing";
      });
    }).toThrow(/references missing scheme/);

    expect(engine.state).toEqual(beforeState);
    expect(values.get(ENGINE_STORAGE_KEY)).toBe(beforeProfile);
    expect(document.getElementById("librespot-engine-palette")?.textContent).toContain(
      "--spice-main: #000000",
    );
  });

  it("rolls back the state and appearance when storage rejects an edit", async () => {
    const state = createDefaultState();
    state.schemes = {
      Dark: { main: "000000", text: "FFFFFF" },
      Light: { main: "FFFFFF", text: "111111" },
    };
    const values = new Map<string, string>();
    let rejectProfileWrite = false;
    const storage: StorageAdapter = {
      get: (key) => values.get(key) ?? null,
      set: (key, value) => {
        if (rejectProfileWrite && key === ENGINE_STORAGE_KEY) {
          throw new Error("profile storage is full");
        }
        values.set(key, value);
      },
      remove: (key) => values.delete(key),
    };
    const store = new EngineStore(storage);
    const persisted = store.save(state);
    const engine = new LibreSpotEngine({
      document,
      window,
      store,
      initialState: persisted,
    });
    await engine.start({ probePerformance: false });
    const beforeState = engine.state;
    const beforeProfile = values.get(ENGINE_STORAGE_KEY);
    rejectProfileWrite = true;

    expect(() => {
      engine.update((draft) => {
        draft.scheme = "Light";
      });
    }).toThrow("profile storage is full");

    expect(engine.state).toEqual(beforeState);
    expect(values.get(ENGINE_STORAGE_KEY)).toBe(beforeProfile);
    expect(document.getElementById("librespot-engine-palette")?.textContent).toContain(
      "--spice-main: #000000",
    );
  });

  it("restores an exact captured state without changing its timestamp", async () => {
    const values = new Map<string, string>();
    const storage: StorageAdapter = {
      get: (key) => values.get(key) ?? null,
      set: (key, value) => values.set(key, value),
      remove: (key) => values.delete(key),
    };
    const original = createDefaultState(new Date("2026-09-01T12:00:00Z"));
    original.schemes = {
      Dark: { main: "000000", text: "FFFFFF" },
      Light: { main: "FFFFFF", text: "111111" },
    };
    const store = new EngineStore(storage);
    const captured = store.restoreExact(original);
    const engine = new LibreSpotEngine({
      document,
      window,
      store,
      initialState: captured,
    });
    await engine.start({ probePerformance: false });

    engine.replace({ ...captured, name: "Temporary replacement" });
    engine.restoreExact(captured);

    expect(engine.state).toEqual(captured);
    expect(JSON.parse(values.get(ENGINE_STORAGE_KEY) ?? "null")).toEqual(captured);
  });

  it("defers a background performance sample without lowering effects", async () => {
    const originalVisibility = Object.getOwnPropertyDescriptor(
      document,
      "visibilityState",
    );
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      value: "hidden",
    });
    try {
      const state = createDefaultState();
      state.autoEffects = true;
      state.effectsTier = "glass";
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

      await expect(engine.probePerformance()).resolves.toBeNull();
      expect(engine.state.effectsTier).toBe("glass");
      expect(engine.state.lastMeasuredFps).toBeNull();
      engine.stop();
    } finally {
      if (originalVisibility) {
        Object.defineProperty(document, "visibilityState", originalVisibility);
      } else {
        Reflect.deleteProperty(document, "visibilityState");
      }
    }
  });
});
