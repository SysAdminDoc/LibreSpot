import type { EngineState } from "../src/core/state.ts";
import type { EngineStore } from "../src/core/store.ts";
import type { SpicetifyApi } from "../src/spicetify-globals.d.ts";

const fixture = vi.hoisted(() => ({
  appliedAdds: 0,
  appliedRemoves: 0,
  engineStarts: 0,
  engines: [] as FakeEngine[],
  failHistoryListener: true,
  historyListenCalls: 0,
  marketplaceUnsubscribes: 0,
  noOpCalls: 0,
  playerAdds: 0,
  playerRemoves: 0,
  stops: 0,
}));

type FixtureState = EngineState;

function recordNoOp(): void {
  fixture.noOpCalls += 1;
}

function createFixtureState(): FixtureState {
  return {
    schemaVersion: 1,
    name: "Fixture",
    theme: "Prism",
    scheme: "Dark",
    schemes: {
      Dark: {
        background: "101010",
        foreground: "FFFFFF",
        accent: "1ED760",
        muted: "A7A7A7",
      },
    },
    layers: {
      palette: true,
      layout: true,
      effects: true,
      accessibility: true,
    },
    effectsTier: "glass",
    autoEffects: true,
    lastMeasuredFps: null,
    dynamicAccent: {
      mode: "album-art",
      preset: "VIBRANT",
      fixed: "1ED760",
      materialPalette: false,
      materialVariant: "tonalSpot",
    },
    appearance: {
      fontFamily: "SpotifyMixUI, CircularSp, sans-serif",
      radius: 12,
      scale: {
        navigation: 1,
        content: 1,
        playbar: 1,
        rightSidebar: 1,
      },
    },
    schedule: {
      enabled: false,
      lightStart: "07:00",
      darkStart: "19:00",
      lightScheme: "Light",
      darkScheme: "Dark",
    },
    enabledSnippets: [],
    featureOverrides: {},
    spotxSwitches: {},
    spicetifyOptions: {},
    userPresets: [],
    homeSections: [],
    sidebarItems: [],
    updatedAt: new Date(0).toISOString(),
  };
}

class FakeEngine extends EventTarget {
  readonly stateValue: FixtureState;
  readonly activeScheme = "Dark";

  public constructor(state: FixtureState) {
    super();
    this.stateValue = state;
    fixture.engines.push(this);
  }

  public get state(): FixtureState {
    return this.stateValue;
  }

  public override addEventListener(
    type: string,
    listener: EventListenerOrEventListenerObject | null,
    options?: boolean | AddEventListenerOptions,
  ): void {
    if (type === "applied") {
      fixture.appliedAdds += 1;
    }
    super.addEventListener(type, listener, options);
  }

  public override removeEventListener(
    type: string,
    listener: EventListenerOrEventListenerObject | null,
    options?: boolean | EventListenerOptions,
  ): void {
    if (type === "applied") {
      fixture.appliedRemoves += 1;
    }
    super.removeEventListener(type, listener, options);
  }

  public start(): void {
    fixture.engineStarts += 1;
  }

  public stop(): void {
    fixture.stops += 1;
  }

  public apply(): void {
    recordNoOp();
  }

  public update(): FixtureState {
    return this.stateValue;
  }

  public refreshAccent(): Promise<void> {
    return Promise.resolve();
  }

  public applyFlags(): Promise<"unavailable"> {
    return Promise.resolve("unavailable");
  }

  public applyPreviewScheme(): void {
    recordNoOp();
  }

  public applyPreviewTheme(): boolean {
    return true;
  }

  public clearPreview(): void {
    recordNoOp();
  }
}

class FakeStore {
  readonly state = createFixtureState();

  public load(): FixtureState {
    return structuredClone(this.state);
  }

  public save(state: FixtureState): FixtureState {
    return structuredClone(state);
  }

  public readQuarantine(): null {
    return null;
  }

  public readRecovery(): null {
    return null;
  }

  public writeRecovery(): void {
    recordNoOp();
  }

  public discardRecovery(): void {
    recordNoOp();
  }

  public readQuarantineRaw(): null {
    return null;
  }

  public discardQuarantine(): void {
    recordNoOp();
  }
}

vi.mock("../src/core/index.ts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../src/core/index.ts")>();
  return {
    ...actual,
    applyHomeArrangement: () => ({ items: [] }),
    applySidebarArrangement: () => ({ items: [] }),
    CATALOG_THEME_STYLES: {},
    createBackup: () => ({}),
    createDefaultState: createFixtureState,
    EngineStore: FakeStore,
    FeatureCapture: class {
    public capture(): void {
      recordNoOp();
    }

      public list(): never[] {
        return [];
      }
    },
    indexedDbMarketplaceStore: () => ({
      readAll: () => Promise.resolve({
        available: true,
        entries: {},
        storage: {
          indexedDb: {},
          localStorage: {},
          indexedDbAvailable: false,
          localStorageAvailable: false,
          indexedDbMigrationComplete: false,
        },
      }),
      subscribeDeleteStatus: (listener: (status: unknown) => void) => {
        listener({ phase: "idle", detail: null });
        return () => {
          fixture.marketplaceUnsubscribes += 1;
        };
      },
    }),
    LibreSpotEngine: FakeEngine,
    parseProfile: () => createFixtureState(),
    parseRestoreSource: () => ({}),
    RECOVERY_RECORD_SCHEMA_VERSION: 1,
    restoreBackupTransaction: () => Promise.resolve({
      marketplaceCount: 0,
      flagsChanged: false,
      flagResult: undefined,
    }),
    runSelfTest: () => ({}),
    serializeBackup: () => "{}",
    serializeProfile: () => "{}",
    SURFACE_SNIPPET_CSS: {},
  };
});

function completeReactApi(): SpicetifyApi["React"] {
  return {
    Fragment: () => undefined,
    createElement: () => undefined,
    useCallback: (callback) => callback,
    useEffect: () => undefined,
    useMemo: (factory) => factory(),
    useState: <T>(initial: T | (() => T)) => [
      typeof initial === "function" ? (initial as () => T)() : initial,
      () => undefined,
    ],
  };
}

function createCompanionApi(): SpicetifyApi {
  const history = {
    location: { pathname: "/librespot" },
    push: () => undefined,
    listen: () => {
      fixture.historyListenCalls += 1;
      if (fixture.failHistoryListener) {
        throw new Error("fixture history registration failure");
      }
      return () => undefined;
    },
  };
  return {
    React: completeReactApi(),
    ReactDOM: {},
    Platform: { History: history },
    LocalStorage: {
      get: () => null,
      set: () => undefined,
    },
    Player: {
      addEventListener: () => {
        fixture.playerAdds += 1;
      },
      removeEventListener: () => {
        fixture.playerRemoves += 1;
      },
    },
  };
}

function installGlobalCompanion(api: Partial<SpicetifyApi>): void {
  window.Spicetify = api;
  (globalThis as Record<string, unknown>).Spicetify = api;
}

async function waitFor(
  predicate: () => boolean,
  message: string,
  timeout = 2_000,
): Promise<void> {
  const deadline = Date.now() + timeout;
  while (!predicate()) {
    if (Date.now() >= deadline) {
      throw new Error(message);
    }
    await new Promise<void>((resolve) => {
      setTimeout(resolve, 10);
    });
  }
}

describe("companion startup integration", () => {
  it("waits for staged APIs, cleans a failed attempt, retries, and leaves probes asynchronous", async () => {
    const originalMutationObserver = globalThis.MutationObserver;
    const disconnects: number[] = [];
    class FixtureMutationObserver {
      public observe(): void {
        recordNoOp();
      }

      public disconnect(): void {
        disconnects.push(1);
      }
    }
    globalThis.MutationObserver = FixtureMutationObserver as unknown as typeof MutationObserver;

    const api = createCompanionApi() as Record<string, unknown> & Partial<SpicetifyApi>;
    delete api.React;
    delete api.Platform;
    delete api.LocalStorage;
    delete api.Player;
    installGlobalCompanion(api);
    const previousOverride = vi.fn(
      (feature: Parameters<NonNullable<SpicetifyApi["expFeatureOverride"]>>[0]) =>
        feature,
    );
    api.expFeatureOverride = previousOverride;
    window.__libreSpotRouteWiring = { marketplace: "unknown" };

    try {
      await import("../src/extensions/librespot-engine.ts");
      await waitFor(
        () => window.__libreSpotEngineStatus?.phase === "loading",
        "companion startup did not publish loading",
      );
      api.React = completeReactApi();
      api.Platform = createCompanionApi().Platform;
      await new Promise<void>((resolve) => {
        setTimeout(resolve, 30);
      });
      expect(fixture.engineStarts).toBe(0);
      api.LocalStorage = createCompanionApi().LocalStorage;
      api.Player = createCompanionApi().Player;

      await waitFor(
        () => window.__libreSpotEngineStatus?.phase === "error",
        "failed fixture startup did not publish an error",
      );
      expect(window.__libreSpotEngineLoaded).toBe(false);
      expect(window.LibreSpot).toBeUndefined();
      expect(fixture.engineStarts).toBe(1);
      expect(fixture.stops).toBe(1);
      expect(fixture.appliedAdds).toBe(1);
      expect(fixture.appliedRemoves).toBe(1);
      expect(fixture.playerAdds).toBe(1);
      expect(fixture.playerRemoves).toBe(1);
      expect(fixture.historyListenCalls).toBe(1);
      expect(fixture.marketplaceUnsubscribes).toBe(1);
      expect(disconnects).toHaveLength(1);
      expect(api.expFeatureOverride).toBe(previousOverride);
      expect(window.__libreSpotRouteWiring).toEqual({ marketplace: "unknown" });

      fixture.failHistoryListener = false;
      window.__libreSpotEngineRetry?.();
      await waitFor(
        () => window.__libreSpotEngineStatus?.phase === "ready",
        "retry did not publish ready",
      );
      expect(window.__libreSpotEngineLoaded).toBe(true);
      expect(window.LibreSpot).toBeDefined();
      expect(fixture.engineStarts).toBe(2);
    } finally {
      Reflect.deleteProperty(window, "LibreSpot");
      Reflect.deleteProperty(window, "__libreSpotEngineRetry");
      Reflect.deleteProperty(window, "__libreSpotEngineStatus");
      Reflect.deleteProperty(window, "__libreSpotEngineLoaded");
      Reflect.deleteProperty(window, "__libreSpotEngineBooting");
      Reflect.deleteProperty(window, "__libreSpotRouteWiring");
      installGlobalCompanion({});
      globalThis.MutationObserver = originalMutationObserver;
    }
  });

  it("does not await a stalled background performance probe", async () => {
    const { LibreSpotEngine } = await import("../src/core/engine.ts");
    const state = createFixtureState();
    const store = {
      save: (next: EngineState) => next,
    } as unknown as EngineStore;
    const engine = new LibreSpotEngine({
      document,
      window,
      store,
      initialState: state,
    });
    const probe = vi
      .spyOn(engine, "probePerformance")
      .mockImplementation(() => new Promise<number | null>(() => undefined));
    try {
      await expect(
        Promise.race([
          engine.start(),
          new Promise<never>((_, reject) => {
            setTimeout(() => reject(new Error("engine start blocked")), 200);
          }),
        ]),
      ).resolves.toBeUndefined();
      expect(probe).toHaveBeenCalledTimes(1);
    } finally {
      probe.mockRestore();
      engine.stop();
    }
  });
});
