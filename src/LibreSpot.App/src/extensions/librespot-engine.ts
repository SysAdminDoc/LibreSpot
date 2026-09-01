import {
  EngineStore,
  FeatureCapture,
  LibreSpotEngine,
  CATALOG_THEME_STYLES,
  createDefaultState,
  parseProfile,
  runSelfTest,
  serializeProfile,
  type EngineState,
  type FeatureOverrideRuntime,
  type HealthReport,
  type RouteState,
  type StorageAdapter,
} from "../core/index.ts";
import type {
  LibreSpotRuntimeApi,
  LibreSpotRuntimeSnapshot,
} from "../spicetify-globals.d.ts";
import {
  BUILTIN_SCHEMES,
  SURFACE_SNIPPET_CSS,
} from "../surface/builtins.ts";
import { panelPath, type PanelId } from "../surface/navigation.ts";

const ACCESS_ICON =
  '<svg viewBox="0 0 24 24" width="16" height="16" fill="none"><path d="M5 5.5h14M5 12h14M5 18.5h14" stroke="currentColor" stroke-width="2" stroke-linecap="round"/><circle cx="9" cy="5.5" r="2.25" fill="currentColor"/><circle cx="15" cy="12" r="2.25" fill="currentColor"/><circle cx="11" cy="18.5" r="2.25" fill="currentColor"/></svg>';
const DESKTOP_BOOTSTRAP_REVISION_KEY = "librespot:desktop-bootstrap-revision";

type DesktopBootstrapPayload = {
  schemaVersion: number;
  profile: unknown;
  enabledSnippets: unknown;
  featureOverrides: unknown;
  spotxSwitches: unknown;
};

function recordValues(value: unknown): Record<string, boolean | number | string> {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return {};
  }
  return Object.fromEntries(
    Object.entries(value).filter(
      (entry): entry is [string, boolean | number | string] =>
        ["boolean", "number", "string"].includes(typeof entry[1]),
    ),
  );
}

function applyDesktopBootstrap(store: EngineStore): EngineState | null {
  const bootstrap = window.__libreSpotDesktopBootstrap;
  if (
    !bootstrap?.payloadBase64 ||
    !/^[a-f0-9]{64}$/.test(bootstrap.revision) ||
    Spicetify.LocalStorage.get(DESKTOP_BOOTSTRAP_REVISION_KEY) === bootstrap.revision
  ) {
    return null;
  }

  try {
    const bytes = Uint8Array.from(atob(bootstrap.payloadBase64), (character) =>
      character.charCodeAt(0),
    );
    const payload = JSON.parse(
      new TextDecoder().decode(bytes),
    ) as DesktopBootstrapPayload;
    if (payload.schemaVersion !== 1) {
      throw new Error(`Unsupported desktop bootstrap ${String(payload.schemaVersion)}.`);
    }

    let state: EngineState;
    if (payload.profile && typeof payload.profile === "object") {
      state = parseProfile(JSON.stringify(payload.profile));
    } else {
      state = store.load();
    }
    state = ensureSchemes(
      Object.keys(state.schemes).length === 0 ? defaultEngineState() : state,
    );
    if (Array.isArray(payload.enabledSnippets)) {
      state.enabledSnippets = payload.enabledSnippets.filter(
        (value): value is string => typeof value === "string",
      );
    }
    state.featureOverrides = recordValues(payload.featureOverrides);
    state.spotxSwitches = recordValues(payload.spotxSwitches);
    const saved = store.save(state);
    Spicetify.LocalStorage.set(
      DESKTOP_BOOTSTRAP_REVISION_KEY,
      bootstrap.revision,
    );
    return saved;
  } catch (error) {
    console.warn("[LibreSpot] Desktop profile bootstrap was rejected.", error);
    return null;
  }
}

function storageAdapter(): StorageAdapter {
  return {
    get: (key) => Spicetify.LocalStorage.get(key),
    set: (key, value) => {
      Spicetify.LocalStorage.set(key, value);
    },
    remove: (key) => {
      if (Spicetify.LocalStorage.remove) {
        Spicetify.LocalStorage.remove(key);
      } else {
        Spicetify.LocalStorage.set(key, "");
      }
    },
  };
}

function defaultEngineState(): EngineState {
  const state = createDefaultState();
  state.schemes = structuredClone(BUILTIN_SCHEMES);
  return state;
}

function ensureSchemes(state: EngineState): EngineState {
  if (Object.keys(state.schemes).length === 0) {
    state.schemes = structuredClone(BUILTIN_SCHEMES);
  }
  if (!state.schemes[state.scheme]) {
    state.scheme = Object.keys(state.schemes)[0] ?? "Dark";
  }
  return state;
}

function artworkUri(): string | undefined {
  const item = Spicetify.Player.data?.item;
  return (
    item?.metadata?.image_xlarge_url ??
    item?.metadata?.image_url ??
    item?.uri
  );
}

function osAccent(): string | null {
  const raw =
    Spicetify.Platform.LocalStorageAPI?.getItem?.("librespot:os-signal") ??
    Spicetify.LocalStorage.get("librespot:os-signal");
  if (!raw) {
    return null;
  }
  try {
    const value: unknown = JSON.parse(raw);
    if (
      typeof value === "object" &&
      value !== null &&
      "accent" in value &&
      typeof value.accent === "string"
    ) {
      return value.accent;
    }
  } catch {
    return null;
  }
  return null;
}

function remoteRuntime(): FeatureOverrideRuntime {
  const api = Spicetify.Platform.RemoteConfigDebugAPI;
  const getProperties = api?.getProperties?.bind(api);
  const setOverrides = api?.setOverrides?.bind(api);
  const setOverride = api?.setOverride?.bind(api);
  const debugApi: NonNullable<FeatureOverrideRuntime["debugApi"]> | undefined = api
    ? {
        ...(getProperties ? { getProperties } : {}),
        ...(setOverrides
          ? {
              setOverrides: (entries, options) =>
                setOverrides(entries, options),
            }
          : {}),
        ...(setOverride ? { setOverride } : {}),
      }
    : undefined;
  const resolverMethod = Spicetify.RemoteConfigResolver?.value?.setOverrides;
  const resolver = resolverMethod
    ? {
        setOverrides: (overrides: Map<string, boolean | number | string>) =>
          resolverMethod(overrides),
      }
    : undefined;
  return {
    ...(debugApi ? { debugApi } : {}),
    ...(resolver ? { resolver } : {}),
  };
}

function installedList(value: string[] | undefined): string[] {
  return [...new Set(value ?? [])].sort((left, right) =>
    left.localeCompare(right),
  );
}

function routeFromWindow(app: "librespot" | "marketplace"): RouteState {
  const explicit = window.__libreSpotRouteWiring?.[app];
  if (explicit) {
    return explicit;
  }
  if (
    app === "librespot" &&
    Spicetify.Platform.History.location.pathname.startsWith("/librespot")
  ) {
    return "wired";
  }
  return "unknown";
}

function spotifyVersion(): string | null {
  return (
    document.documentElement.getAttribute("data-spotify-version") ??
    Spicetify.LocalStorage.get("librespot:spotify-version")
  );
}

async function probeBundleRoute(app: "librespot" | "marketplace"): Promise<RouteState> {
  if (
    app === "librespot" &&
    Spicetify.Platform.History.location.pathname.startsWith("/librespot")
  ) {
    return "wired";
  }
  const configured = installedList(Spicetify.Config?.custom_apps).includes(app);
  if (!configured) {
    return "not-wired";
  }
  try {
    const response = await fetch(
      `/xpui.js?librespot-health=${Date.now()}`,
      { cache: "no-store" },
    );
    if (!response.ok) {
      return "unknown";
    }
    const source = await response.text();
    return source.includes(`spicetify-routes-${app}`)
      ? "wired"
      : "not-wired";
  } catch {
    return "unknown";
  }
}

async function copyThroughPlatform(value: string): Promise<void> {
  const clipboard = Spicetify.Platform.ClipboardAPI;
  if (clipboard?.copy) {
    await clipboard.copy(value);
    return;
  }
  if (clipboard?.copyText) {
    await clipboard.copyText(value);
    return;
  }
  throw new Error("Spotify's clipboard API is unavailable.");
}

function notify(message: string, error = false): void {
  Spicetify.showNotification?.(message, error);
}

function navEntryPresent(): boolean {
  return Boolean(
    document.querySelector(
      'a[href="/librespot"], a[href^="/librespot/"], [aria-label="LibreSpot"], [title="LibreSpot"]',
    ),
  );
}

function registerAccessEntries(): void {
  const open = () => {
    Spicetify.Platform.History.push(panelPath("look"));
  };
  try {
    const MenuItem = Spicetify.Menu?.Item;
    if (!MenuItem) {
      throw new Error("Menu API unavailable.");
    }
    new MenuItem("LibreSpot", false, open, ACCESS_ICON).register();
  } catch {
    console.warn("[LibreSpot] Profile-menu entry could not be registered.");
  }
  window.setTimeout(() => {
    if (navEntryPresent()) {
      return;
    }
    try {
      const TopbarButton = Spicetify.Topbar?.Button;
      if (!TopbarButton) {
        throw new Error("Topbar API unavailable.");
      }
      new TopbarButton("LibreSpot", ACCESS_ICON, open);
    } catch {
      console.warn("[LibreSpot] Topbar fallback could not be registered.");
    }
  }, 4000);
}

async function waitForApi(): Promise<void> {
  for (let attempt = 0; attempt < 300; attempt += 1) {
    const api = window.Spicetify;
    if (
      api?.React &&
      api.ReactDOM &&
      api.Platform?.History &&
      api.LocalStorage &&
      api.Player
    ) {
      return;
    }
    await new Promise<void>((resolve) => {
      window.setTimeout(resolve, 100);
    });
  }
  throw new Error("Spotify APIs did not become ready.");
}

async function bootstrap(): Promise<void> {
  if (window.__libreSpotEngineLoaded) {
    return;
  }
  window.__libreSpotEngineLoaded = true;
  try {
    await waitForApi();
    const store = new EngineStore(storageAdapter());
    let initial = applyDesktopBootstrap(store) ?? store.load();
    if (Object.keys(initial.schemes).length === 0) {
      initial = store.save(defaultEngineState());
    } else {
      initial = ensureSchemes(initial);
    }
    const capture = new FeatureCapture();
    const engine = new LibreSpotEngine({
      document,
      window,
      store,
      initialState: initial,
      snippetCss: SURFACE_SNIPPET_CSS,
      themeStyles: CATALOG_THEME_STYLES,
      featureRuntime: remoteRuntime(),
      colorExtractor: async (uri) => {
        if (!Spicetify.colorExtractor) {
          return null;
        }
        return await Spicetify.colorExtractor(uri);
      },
      artworkUri,
      osAccent,
    });
    let librespotRoute = routeFromWindow("librespot");
    let marketplaceRoute = routeFromWindow("marketplace");
    let health: HealthReport = runHealth();
    const listeners = new Set<(snapshot: LibreSpotRuntimeSnapshot) => void>();

    function runHealth(): HealthReport {
      return runSelfTest({
        document,
        spotifyVersion: spotifyVersion(),
        librespotRoute,
        marketplaceRoute,
      });
    }

    function snapshot(): LibreSpotRuntimeSnapshot {
      return {
        state: engine.state,
        activeScheme: engine.activeScheme,
        health,
        features: capture.list(),
        installedExtensions: installedList(Spicetify.Config?.extensions),
        installedCustomApps: installedList(Spicetify.Config?.custom_apps),
      };
    }

    const previousOverride = Spicetify.expFeatureOverride;
    Spicetify.expFeatureOverride = (feature) => {
      const resolved = previousOverride ? previousOverride(feature) : feature;
      capture.capture(resolved);
      const override = engine.state.featureOverrides[resolved.name];
      if (override !== undefined) {
        resolved.default = override;
      }
      emit();
      return resolved;
    };

    function emit(): void {
      const current = snapshot();
      for (const listener of listeners) {
        listener(current);
      }
    }

    async function refreshRoutes(): Promise<void> {
      const [librespot, marketplace] = await Promise.all([
        probeBundleRoute("librespot"),
        probeBundleRoute("marketplace"),
      ]);
      librespotRoute = librespot;
      marketplaceRoute = marketplace;
      window.__libreSpotRouteWiring = { librespot, marketplace };
      health = runHealth();
      emit();
    }

    const runtime: LibreSpotRuntimeApi = {
      getSnapshot: snapshot,
      subscribe: (listener) => {
        listeners.add(listener);
        listener(snapshot());
        return () => {
          listeners.delete(listener);
        };
      },
      update: async (mutator, notice) => {
        const beforeFlags = JSON.stringify(engine.state.featureOverrides);
        const next = engine.update(mutator);
        await engine.refreshAccent();
        if (JSON.stringify(next.featureOverrides) !== beforeFlags) {
          await engine.applyFlags();
        }
        health = runHealth();
        emit();
        if (notice) {
          notify(notice);
        }
        return next;
      },
      previewScheme: (name) => {
        const scheme = engine.state.schemes[name];
        if (scheme) {
          engine.applyPreviewScheme(scheme);
        }
      },
      clearPreview: () => {
        engine.clearPreview();
      },
      refreshHealth: () => {
        health = runHealth();
        emit();
        void refreshRoutes();
        return health;
      },
      copyProfile: async () => {
        try {
          await copyThroughPlatform(serializeProfile(engine.state));
          notify("Profile copied. Import it in LibreSpot Desktop.");
        } catch (error) {
          const message =
            error instanceof Error ? error.message : "Profile copy failed.";
          notify(message, true);
        }
      },
      copyDiagnostics: async () => {
        try {
          const diagnostics = {
            generatedAt: new Date().toISOString(),
            spotifyVersion: spotifyVersion(),
            spicetifyVersion: Spicetify.Config?.version ?? null,
            activeScheme: engine.activeScheme,
            effectsTier: engine.state.effectsTier,
            installedExtensions: installedList(Spicetify.Config?.extensions),
            installedCustomApps: installedList(Spicetify.Config?.custom_apps),
            health,
          };
          await copyThroughPlatform(`${JSON.stringify(diagnostics, null, 2)}\n`);
          notify("Diagnostics copied.");
        } catch (error) {
          const message =
            error instanceof Error ? error.message : "Diagnostics copy failed.";
          notify(message, true);
        }
      },
      openPanel: (panel) => {
        Spicetify.Platform.History.push(panelPath(panel as PanelId));
      },
    };

    window.LibreSpot = runtime;
    engine.addEventListener("applied", emit);
    await engine.start();
    health = runHealth();
    emit();

    const onSongChange = () => {
      void engine.refreshAccent().then(emit);
    };
    Spicetify.Player.addEventListener("songchange", onSongChange);
    Spicetify.Platform.History.listen?.(() => {
      engine.apply();
      health = runHealth();
      emit();
      void refreshRoutes();
    });
    window.setInterval(() => {
      engine.apply();
      emit();
    }, 60_000);
    registerAccessEntries();
    await refreshRoutes();
    console.info("[LibreSpot] live engine ready");
  } catch (error) {
    window.__libreSpotEngineLoaded = false;
    const message =
      error instanceof Error ? error.message : "Live engine failed to start.";
    console.error("[LibreSpot]", error);
    notify(`LibreSpot engine: ${message}`, true);
  }
}

void bootstrap();
