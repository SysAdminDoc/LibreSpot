import type {
  CapturedFeature,
  EngineState,
  HealthReport,
} from "./core/index.ts";

export type UiNode = unknown;
export type UiElementType =
  | string
  | ((properties: Record<string, unknown>) => UiNode);

export type HistoryLocation = {
  pathname: string;
};

export type HistoryApi = {
  location: HistoryLocation;
  push(path: string): void;
  listen?(listener: (location: HistoryLocation) => void): () => void;
};

export type ClipboardApi = {
  copy?(value: string): unknown;
  copyText?(value: string): unknown;
};

export type SpicetifyApi = {
  React: {
    Fragment: UiElementType;
    createElement(
      type: UiElementType,
      properties?: Record<string, unknown> | null,
      ...children: UiNode[]
    ): UiNode;
    useCallback<T extends (...arguments_: never[]) => unknown>(
      callback: T,
      dependencies: readonly unknown[],
    ): T;
    useEffect(
      effect: () => void | (() => void),
      dependencies?: readonly unknown[],
    ): void;
    useMemo<T>(factory: () => T, dependencies: readonly unknown[]): T;
    useState<T>(initial: T | (() => T)): [
      T,
      (value: T | ((current: T) => T)) => void,
    ];
  };
  ReactDOM: unknown;
  LocalStorage: {
    get(key: string): string | null;
    set(key: string, value: string): void;
    remove?(key: string): void;
  };
  Platform: {
    History: HistoryApi;
    ClipboardAPI?: ClipboardApi;
    RemoteConfigDebugAPI?: {
      getProperties?: () => Promise<
        {
          source: string;
          type: string;
          name: string;
          localValue?: boolean | number | string;
        }[]
      >;
      setOverrides?: (...arguments_: unknown[]) => Promise<unknown>;
      setOverride?: (...arguments_: unknown[]) => Promise<unknown>;
    };
    RemoteConfiguration?: unknown;
    LocalStorageAPI?: {
      getItem?(key: string): string | null;
      setItem?(key: string, value: string): void;
    };
  };
  RemoteConfigResolver?: {
    value?: {
      setOverrides?: (
        overrides: Map<string, boolean | number | string>,
      ) => unknown;
      remoteConfiguration?: unknown;
    };
  };
  Player: {
    data?: {
      item?: {
        uri?: string;
        metadata?: Record<string, string | undefined>;
      };
    };
    addEventListener(name: string, listener: () => void): void;
    removeEventListener?(name: string, listener: () => void): void;
  };
  Config?: {
    version?: string;
    current_theme?: string;
    color_scheme?: string;
    extensions?: string[];
    custom_apps?: string[];
  };
  colorExtractor?(
    imageOrUri: string,
  ): Promise<Record<string, string> | null | undefined>;
  expFeatureOverride?: (feature: CapturedFeature) => CapturedFeature;
  showNotification?(message: string, isError?: boolean): void;
  SVGIcons?: Record<string, string>;
  Topbar?: {
    Button: new (
      label: string,
      icon: string,
      onClick: () => void,
      disabled?: boolean,
    ) => unknown;
  };
  Menu?: {
    Item: new (
      label: string,
      checked: boolean,
      onClick: () => void,
      icon?: string,
    ) => { register(): void };
  };
};

export type LibreSpotRuntimeSnapshot = {
  state: EngineState;
  activeScheme: string;
  health: HealthReport;
  features: CapturedFeature[];
  installedExtensions: string[];
  installedCustomApps: string[];
};

export type LibreSpotRuntimeApi = {
  getSnapshot(): LibreSpotRuntimeSnapshot;
  subscribe(listener: (snapshot: LibreSpotRuntimeSnapshot) => void): () => void;
  update(
    mutator: (draft: EngineState) => void,
    notice?: string,
  ): Promise<EngineState>;
  previewScheme(name: string): void;
  clearPreview(): void;
  refreshHealth(): HealthReport;
  copyProfile(): Promise<void>;
  copyDiagnostics(): Promise<void>;
  openPanel(panel: string): void;
};

declare global {
  const Spicetify: SpicetifyApi;

  interface Window {
    Spicetify?: Partial<SpicetifyApi>;
    LibreSpot?: LibreSpotRuntimeApi;
    __libreSpotEngineLoaded?: boolean;
    __libreSpotDesktopBootstrap?: {
      payloadBase64: string;
      revision: string;
    };
    __libreSpotRouteWiring?: {
      librespot?: "wired" | "not-wired" | "unknown";
      marketplace?: "wired" | "not-wired" | "unknown";
    };
  }
}
