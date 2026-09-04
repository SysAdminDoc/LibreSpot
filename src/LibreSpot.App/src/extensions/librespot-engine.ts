import {
  applyHomeArrangement,
  applySidebarArrangement,
  EngineStore,
  FeatureCapture,
  LibreSpotEngine,
  CATALOG_THEME_STYLES,
  createBackup,
  createDefaultState,
  indexedDbMarketplaceStore,
  parseBackup,
  parseProfile,
  serializeBackup,
  runSelfTest,
  serializeProfile,
  type EngineState,
  type ArrangementItem,
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
import settingsIconSource from "lucide-static/icons/settings.svg";
import brandIconSource from "../icons/librespot.generated.txt";
import { panelPath, type PanelId } from "../surface/navigation.ts";

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
  if (app === "marketplace" && !installedList(Spicetify.Config?.custom_apps).includes(app)) {
    return "inactive";
  }
  return "unknown";
}

function spotifyVersion(): string | null {
  return (
    document.documentElement.getAttribute("data-spotify-version") ??
    Spicetify.Platform.version ??
    Spicetify.Platform.PlatformData?.client_version_triple ??
    Spicetify.Platform.PlatformData?.event_sender_context_information
      ?.client_version_string ??
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
    return app === "marketplace" ? "inactive" : "not-wired";
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

let activeNotificationKey: string | number | undefined;
let pendingNotificationTimer: number | undefined;
let notificationReadyAt = 0;

function notify(message: string, error = false): void {
  const snackbar = Spicetify.Snackbar;
  if (snackbar) {
    if (pendingNotificationTimer !== undefined) {
      window.clearTimeout(pendingNotificationTimer);
    }
    const duration = error ? 4_000 : 2_200;
    const show = () => {
      pendingNotificationTimer = undefined;
      if (activeNotificationKey !== undefined) {
        snackbar.closeSnackbar(activeNotificationKey);
        activeNotificationKey = undefined;
        notificationReadyAt = Date.now() + 360;
      }
      const remainingExitTime = notificationReadyAt - Date.now();
      if (remainingExitTime > 0) {
        pendingNotificationTimer = window.setTimeout(show, remainingExitTime);
        return;
      }
      const key = snackbar.enqueueSnackbar(message, {
        variant: error ? "error" : "default",
        autoHideDuration: duration,
        preventDuplicate: true,
      });
      activeNotificationKey = key;
      window.setTimeout(() => {
        if (activeNotificationKey === key) {
          activeNotificationKey = undefined;
        }
      }, duration + 500);
    };
    pendingNotificationTimer = window.setTimeout(show, 90);
    return;
  }
  Spicetify.showNotification?.(message, error, error ? 4_000 : 2_200);
}

function settingsEntryPresent(): boolean {
  return Boolean(
    document.querySelector(
      '[aria-label="LibreSpot Settings"], [title="LibreSpot Settings"]',
    ),
  );
}

function registerAccessEntries(): void {
  const openStore = () => {
    Spicetify.Platform.History.push(panelPath("store"));
  };
  const openSettings = () => {
    Spicetify.Platform.History.push(panelPath("look"));
  };
  try {
    const MenuItem = Spicetify.Menu?.Item;
    if (!MenuItem) {
      throw new Error("Menu API unavailable.");
    }
    new MenuItem("LibreSpot Store", false, openStore, brandIconSource).register();
  } catch {
    console.warn("[LibreSpot] Profile-menu entry could not be registered.");
  }
  window.setTimeout(() => {
    if (settingsEntryPresent()) {
      return;
    }
    try {
      const TopbarButton = Spicetify.Topbar?.Button;
      if (!TopbarButton) {
        throw new Error("Topbar API unavailable.");
      }
      new TopbarButton("LibreSpot Settings", settingsIconSource, openSettings);
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

function runtimeIsReady(): boolean {
  return window.LibreSpot !== undefined;
}

async function bootstrap(): Promise<void> {
  if (runtimeIsReady()) {
    return;
  }
  window.__libreSpotEngineLoaded = false;
  let claimedRuntime: LibreSpotRuntimeApi | undefined;
  try {
    await waitForApi();
    // Spicetify loads this file once as the always-on companion and can load it
    // again as a custom-app subfile. Let both attempts reach API readiness, then
    // allow the first complete runtime to own the page.
    if (runtimeIsReady()) {
      return;
    }
    registerAccessEntries();
    const store = new EngineStore(storageAdapter());
    const stored = store.load();
    let initial = applyDesktopBootstrap(store) ?? stored;
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
    let availableHomeSections: ArrangementItem[] = [];
    let availableSidebarItems: ArrangementItem[] = [];
    let arrangementTimer: number | undefined;
    const listeners = new Set<(snapshot: LibreSpotRuntimeSnapshot) => void>();

    function sameArrangementItems(
      left: readonly ArrangementItem[],
      right: readonly ArrangementItem[],
    ): boolean {
      return (
        left.length === right.length &&
        left.every((item, index) => {
          const candidate = right.at(index);
          return candidate?.id === item.id && candidate.label === item.label;
        })
      );
    }

    function refreshArrangements(): boolean {
      const state = engine.state;
      const home = applyHomeArrangement(document, state.homeSections);
      const sidebar = applySidebarArrangement(document, state.sidebarItems);
      let changed = false;
      if (
        home.items.length > 0 &&
        !sameArrangementItems(availableHomeSections, home.items)
      ) {
        availableHomeSections = home.items;
        changed = true;
      }
      if (
        sidebar.items.length > 0 &&
        !sameArrangementItems(availableSidebarItems, sidebar.items)
      ) {
        availableSidebarItems = sidebar.items;
        changed = true;
      }
      return changed;
    }

    function scheduleArrangementRefresh(delay = 100): void {
      if (arrangementTimer !== undefined) {
        window.clearTimeout(arrangementTimer);
      }
      arrangementTimer = window.setTimeout(() => {
        arrangementTimer = undefined;
        if (refreshArrangements()) {
          emit();
        }
      }, delay);
    }

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
        availableHomeSections,
        availableSidebarItems,
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

    const marketplaceStore = indexedDbMarketplaceStore(window.indexedDB);

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
        const beforeFlags = engine.state.featureOverrides;
        const next = engine.update(mutator);
        await engine.refreshAccent();
        if (
          JSON.stringify(next.featureOverrides) !== JSON.stringify(beforeFlags)
        ) {
          await engine.applyFlags(beforeFlags);
        }
        refreshArrangements();
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
      previewTheme: (name, scheme) => engine.applyPreviewTheme(name, scheme),
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
      backupState: async () => {
        try {
          const marketplace = await marketplaceStore.readAll();
          if (!marketplace.available) {
            // Never present an unreadable database as an empty one: someone would
            // keep that file, wipe their profile, and lose every Marketplace setting.
            notify(
              "Marketplace's settings could not be read, so no backup was taken. Close any other Spotify window and try again.",
              true,
            );
            return;
          }

          const file = serializeBackup(
            createBackup(engine.state, marketplace.entries, new Date()),
          );
          await copyThroughPlatform(file);
          const count = Object.keys(marketplace.entries).length;
          notify(
            count > 0
              ? `Backup copied: this profile and ${count} Marketplace settings. Paste it into a file to keep it.`
              : "Backup copied: this profile. Marketplace has nothing saved yet.",
          );
        } catch (error) {
          const message =
            error instanceof Error ? error.message : "Backup failed.";
          notify(message, true);
        }
      },
      restoreState: async (source) => {
        let engineRestored = false;
        try {
          const restored = parseBackup(source);
          const count = Object.keys(restored.marketplace).length;

          // Marketplace first: it is the half that can refuse. Overwriting the
          // profile and then failing would leave the user worse off with a
          // message that reads like nothing happened.
          if (count > 0) {
            await marketplaceStore.writeAll(restored.marketplace);
          }
          await runtime.update((draft) => {
            Object.assign(draft, restored.engine);
          });
          engineRestored = true;

          notify(
            count > 0
              ? `Restored this profile and ${count} Marketplace settings. Reload Spotify to see Marketplace pick them up.`
              : "Restored this profile.",
          );
        } catch (error) {
          const message =
            error instanceof Error ? error.message : "Restore failed.";
          notify(
            engineRestored ? `This profile was restored, but ${message}` : message,
            true,
          );
        }
      },
      reportError: (message) => {
        notify(message, true);
      },
      openPanel: (panel) => {
        Spicetify.Platform.History.push(panelPath(panel as PanelId));
      },
      openDesktopStore: (kind, id, scheme) => {
        const parameters = new URLSearchParams({ kind, id });
        if (scheme) parameters.set("scheme", scheme);
        const anchor = document.createElement("a");
        anchor.href = `librespot://store?${parameters.toString()}`;
        anchor.target = "_self";
        anchor.hidden = true;
        document.body.append(anchor);
        anchor.click();
        anchor.remove();
        notify(`Opening ${id} in LibreSpot Desktop.`);
      },
    };

    claimedRuntime = runtime;
    window.LibreSpot = runtime;
    engine.addEventListener("applied", emit);
    await engine.start({
      previousFeatureOverrides: stored.featureOverrides,
    });
    window.__libreSpotEngineLoaded = true;
    refreshArrangements();
    health = runHealth();
    emit();

    const arrangementObserver = new MutationObserver(() => {
      scheduleArrangementRefresh();
    });
    const arrangementRoots = [
      document.querySelector(".Root__main-view"),
      document.querySelector(".Root__nav-bar"),
    ].filter((element): element is Element => element !== null);
    for (const root of arrangementRoots) {
      arrangementObserver.observe(root, { childList: true, subtree: true });
    }

    const onSongChange = () => {
      void engine.refreshAccent().then(emit);
    };
    Spicetify.Player.addEventListener("songchange", onSongChange);
    Spicetify.Platform.History.listen?.(() => {
      engine.apply();
      health = runHealth();
      emit();
      scheduleArrangementRefresh();
      window.setTimeout(() => {
        scheduleArrangementRefresh(0);
      }, 900);
      void refreshRoutes();
    });
    window.setInterval(() => {
      engine.apply();
      refreshArrangements();
      emit();
    }, 60_000);
    await refreshRoutes();
    console.info("[LibreSpot] live engine ready");
  } catch (error) {
    if (claimedRuntime && window.LibreSpot === claimedRuntime) {
      delete window.LibreSpot;
    }
    window.__libreSpotEngineLoaded = false;
    const message =
      error instanceof Error ? error.message : "Live engine failed to start.";
    console.error("[LibreSpot]", error);
    notify(`LibreSpot engine: ${message}`, true);
  }
}

void bootstrap();
