import "./app.css";
import bookmarkIcon from "lucide-static/icons/bookmark.svg";
import heartPulseIcon from "lucide-static/icons/heart-pulse.svg";
import paletteIcon from "lucide-static/icons/palette.svg";
import slidersHorizontalIcon from "lucide-static/icons/sliders-horizontal.svg";
import storeIcon from "lucide-static/icons/store.svg";
import toggleLeftIcon from "lucide-static/icons/toggle-left.svg";
import brandIconSource from "./icons/librespot.generated.txt";
import { FeaturesPanel } from "./panels/features.ts";
import { HealthPanel } from "./panels/health.ts";
import { LookPanel } from "./panels/look.ts";
import { PresetsPanel } from "./panels/presets.ts";
import { StorePanel } from "./panels/store.ts";
import { TweaksPanel } from "./panels/tweaks.ts";
import type {
  LibreSpotEngineBootstrapStatus,
  LibreSpotRuntimeApi,
  LibreSpotRuntimeSnapshot,
  SpicetifyApi,
  UiNode,
} from "./spicetify-globals.d.ts";
import {
  PANEL_DEFINITIONS,
  panelFromPath,
  panelPath,
  type PanelDefinition,
  type PanelId,
} from "./surface/navigation.ts";
import type {
  PanelComponent,
  PanelProperties,
} from "./surface/panel-types.ts";
import { readReadyRuntime } from "./surface/runtime-readiness.ts";
import { h } from "./surface/ui.ts";

const PANELS: Record<PanelId, PanelComponent> = {
  store: StorePanel,
  look: LookPanel,
  tweaks: TweaksPanel,
  features: FeaturesPanel,
  presets: PresetsPanel,
  health: HealthPanel,
};

const PANEL_ICONS: Record<PanelDefinition["icon"], string> = {
  store: storeIcon,
  look: paletteIcon,
  tweaks: slidersHorizontalIcon,
  features: toggleLeftIcon,
  presets: bookmarkIcon,
  health: heartPulseIcon,
};

const ENGINE_STATUS_EVENT = "librespot-engine-status";
const DEFAULT_ENGINE_STATUS: LibreSpotEngineBootstrapStatus = {
  phase: "loading",
  message: null,
  attempt: 0,
  revision: 0,
};

function PanelIcon(properties: { icon: PanelDefinition["icon"] }): UiNode {
  return h("span", {
    className: "librespot-rail__icon",
    "aria-hidden": "true",
    dangerouslySetInnerHTML: { __html: PANEL_ICONS[properties.icon] },
  });
}

function readEngineStatus(): LibreSpotEngineBootstrapStatus {
  const status = window.__libreSpotEngineStatus;
  return status ?? DEFAULT_ENGINE_STATUS;
}

function useEngineStatus(): LibreSpotEngineBootstrapStatus {
  const React = Spicetify.React;
  const [status, setStatus] = React.useState(readEngineStatus);
  React.useEffect(() => {
    const sync = () => {
      setStatus(readEngineStatus());
    };
    window.addEventListener(ENGINE_STATUS_EVENT, sync);
    sync();
    return () => {
      window.removeEventListener(ENGINE_STATUS_EVENT, sync);
    };
  }, []);
  return status;
}

function useRuntime(): LibreSpotRuntimeApi | null {
  const React = Spicetify.React;
  const [runtime, setRuntime] = React.useState(readReadyRuntime);
  React.useEffect(() => {
    const sync = () => {
      setRuntime(readReadyRuntime());
    };
    window.addEventListener(ENGINE_STATUS_EVENT, sync);
    sync();
    return () => {
      window.removeEventListener(ENGINE_STATUS_EVENT, sync);
    };
  }, []);
  return runtime;
}

function useSnapshot(
  runtime: LibreSpotRuntimeApi | null,
): LibreSpotRuntimeSnapshot | null {
  const React = Spicetify.React;
  const [snapshot, setSnapshot] =
    React.useState<LibreSpotRuntimeSnapshot | null>(() =>
      runtime?.getSnapshot() ?? null,
    );
  React.useEffect(() => {
    if (!runtime) {
      return;
    }
    setSnapshot(runtime.getSnapshot());
    return runtime.subscribe(setSnapshot);
  }, [runtime]);
  return snapshot;
}

type PanelErrorBoundaryProperties = PanelProperties & {
  children?: UiNode;
  panelLabel: string;
  key?: string;
};

type PanelErrorBoundaryState = {
  error: unknown;
};

type PanelErrorBoundaryInstance = {
  props: PanelErrorBoundaryProperties;
  state: PanelErrorBoundaryState;
  setState(next: PanelErrorBoundaryState): void;
};

type PanelErrorBoundaryBase = new (
  properties: PanelErrorBoundaryProperties,
) => PanelErrorBoundaryInstance;

type PanelErrorBoundaryComponent = (
  properties: PanelErrorBoundaryProperties,
) => UiNode;

let panelErrorBoundaryReact: SpicetifyApi["React"] | undefined;
let panelErrorBoundaryComponent: PanelErrorBoundaryComponent | undefined;

function PanelErrorSurface(properties: {
  panelLabel: string;
  onRetry: () => void;
  onOpenHealth: () => void;
}): UiNode {
  return h(
    "section",
    {
      className: "librespot-panel-error",
      role: "alert",
      "aria-live": "assertive",
    },
    h("h2", null, `${properties.panelLabel} panel unavailable`),
    h(
      "p",
      null,
      "LibreSpot could not display this panel. Your saved settings are still safe.",
    ),
    h(
      "div",
      { className: "librespot-panel-error__actions" },
      h(
        "button",
        {
          type: "button",
          className: "librespot-button",
          onClick: properties.onRetry,
        },
        "Retry panel",
      ),
      h(
        "button",
        {
          type: "button",
          className: "librespot-button librespot-button--secondary",
          onClick: properties.onOpenHealth,
        },
        "Open Health",
      ),
    ),
  );
}

function PanelErrorBoundaryFallback(
  properties: PanelErrorBoundaryProperties,
): UiNode {
  return properties.children;
}

function panelErrorBoundaryFor(
  React: SpicetifyApi["React"],
): PanelErrorBoundaryComponent {
  if (panelErrorBoundaryReact === React && panelErrorBoundaryComponent) {
    return panelErrorBoundaryComponent;
  }
  const BaseComponent = (
    React as unknown as { Component?: PanelErrorBoundaryBase }
  ).Component;
  if (!BaseComponent) {
    return PanelErrorBoundaryFallback;
  }
  class PanelErrorBoundary extends BaseComponent {
    public override state: PanelErrorBoundaryState = { error: null };

    public static getDerivedStateFromError(
      error: unknown,
    ): PanelErrorBoundaryState {
      return { error };
    }

    public componentDidCatch(error: unknown): void {
      const detail = error instanceof Error ? error.message : "unknown error";
      console.error(`[LibreSpot] ${this.props.panelLabel} panel failed: ${detail}`);
    }

    public render(): UiNode {
      if (this.state.error !== null) {
        return h(PanelErrorSurface, {
          panelLabel: this.props.panelLabel,
          onRetry: () => {
            this.setState({ error: null });
          },
          onOpenHealth: () => {
            this.props.runtime.openPanel("health");
          },
        });
      }
      return this.props.children;
    }
  }
  panelErrorBoundaryReact = React;
  panelErrorBoundaryComponent =
    PanelErrorBoundary as unknown as PanelErrorBoundaryComponent;
  return panelErrorBoundaryComponent;
}

function LoadingSurface(properties: {
  status: LibreSpotEngineBootstrapStatus;
}): UiNode {
  return h(
    "main",
    {
      className: "librespot-app librespot-loading",
      "data-librespot-app": "loading",
    },
    h("div", { className: "librespot-loading__mark", "aria-hidden": "true" }),
    h("h1", null, "LibreSpot"),
    h(
      "p",
      null,
      properties.status.attempt > 0
        ? "Waiting for Spotify's live APIs to become available."
        : "Waiting for the live engine to finish loading.",
    ),
  );
}

function EngineErrorSurface(properties: {
  status: LibreSpotEngineBootstrapStatus;
}): UiNode {
  const retry = window.__libreSpotEngineRetry;
  return h(
    "main",
    {
      className: "librespot-app librespot-loading librespot-loading--error",
      "data-librespot-app": "error",
    },
    h("div", { className: "librespot-loading__mark is-error", "aria-hidden": "true" }),
    h("h1", null, "LibreSpot could not start"),
    h(
      "div",
      {
        className: "librespot-loading__error",
        role: "alert",
        "aria-live": "assertive",
      },
      h("p", null, properties.status.message ?? "The live engine stopped before it was ready."),
      retry
        ? h(
            "button",
            {
              type: "button",
              className: "librespot-button",
              onClick: () => {
                retry();
              },
            },
            "Retry engine startup",
          )
        : null,
    ),
  );
}

function usePanel(): PanelId {
  const React = Spicetify.React;
  const history = Spicetify.Platform.History;
  const [panel, setPanel] = React.useState<PanelId>(() =>
    panelFromPath(history.location.pathname),
  );
  React.useEffect(() => {
    const update = (location: { pathname: string }) => {
      setPanel(panelFromPath(location.pathname));
    };
    const unsubscribe = history.listen?.(update);
    const onPopState = () => {
      setPanel(panelFromPath(history.location.pathname));
    };
    window.addEventListener("popstate", onPopState);
    return () => {
      unsubscribe?.();
      window.removeEventListener("popstate", onPopState);
    };
  }, [history]);
  return panel;
}

function AppShell(properties: PanelProperties & { activePanel: PanelId }): UiNode {
  const brandIconMask = `url("data:image/svg+xml,${encodeURIComponent(brandIconSource)}")`;
  const activeDefinition = PANEL_DEFINITIONS.find(
    (panel) => panel.id === properties.activePanel,
  );
  const Panel = PANELS[properties.activePanel];
  const PanelErrorBoundary = panelErrorBoundaryFor(Spicetify.React);
  const problemCount = properties.snapshot.health.checks.filter(
    (check) => check.status === "broken" || check.status === "warning",
  ).length;

  return h(
    "main",
    {
      className: "librespot-app",
      "data-librespot-app": "ready",
      "data-active-panel": properties.activePanel,
    },
    h(
      "aside",
      { className: "librespot-rail", "aria-label": "LibreSpot sections" },
      h(
        "div",
        { className: "librespot-brand" },
        h(
          "span",
          {
            className: "librespot-brand__mark",
            "aria-hidden": "true",
            style: {
              maskImage: brandIconMask,
              WebkitMaskImage: brandIconMask,
            },
          },
        ),
        h(
          "div",
          null,
          h("strong", null, "LibreSpot"),
          h("span", null, "Live customization"),
        ),
      ),
      h(
        "nav",
        { className: "librespot-rail__nav" },
        ...PANEL_DEFINITIONS.map((panel) =>
          h(
            "button",
            {
              type: "button",
              key: panel.id,
              className:
                panel.id === properties.activePanel
                  ? "librespot-rail__item is-active"
                  : "librespot-rail__item",
              "aria-current":
                panel.id === properties.activePanel ? "page" : undefined,
              onClick: () => {
                properties.runtime.openPanel(panel.id);
              },
            },
            h(
              "span",
              { className: "librespot-rail__icon-wrap", "aria-hidden": "true" },
              h(PanelIcon, { icon: panel.icon }),
            ),
            h(
              "span",
              { className: "librespot-rail__copy" },
              h(
                "span",
                { className: "librespot-rail__label" },
                panel.label,
                panel.id === "health" && problemCount > 0
                  ? h(
                      "span",
                      {
                        className: "librespot-rail__count",
                        "aria-label": `${problemCount} health warnings`,
                      },
                      String(problemCount),
                    )
                  : null,
              ),
              h("span", { className: "librespot-rail__description" }, panel.description),
            ),
          ),
        ),
      ),
      h(
        "div",
        { className: "librespot-rail__footer" },
        h(
          "span",
          {
            className: properties.snapshot.health.healthy
              ? "librespot-health-dot is-healthy"
              : "librespot-health-dot is-warning",
            "aria-hidden": "true",
          },
        ),
        h(
          "span",
          null,
          properties.snapshot.health.healthy ? "Engine ready" : "Check Health",
        ),
      ),
    ),
    h(
      "div",
      { className: "librespot-content" },
      h(
        "div",
        { className: "librespot-content__crumb" },
        h("span", null, "LibreSpot"),
        h("span", { "aria-hidden": "true" }, "/"),
        h("strong", null, activeDefinition?.label ?? "Look"),
      ),
      h(
        PanelErrorBoundary,
        {
          key: properties.activePanel,
          panelLabel: activeDefinition?.label ?? "Selected",
          runtime: properties.runtime,
          snapshot: properties.snapshot,
        },
        h(Panel, {
          runtime: properties.runtime,
          snapshot: properties.snapshot,
        }),
      ),
    ),
  );
}

export default function LibreSpotApp(): UiNode {
  const status = useEngineStatus();
  const runtime = useRuntime();
  const snapshot = useSnapshot(runtime);
  const activePanel = usePanel();
  if (!runtime || !snapshot) {
    return h(
      status.phase === "error" ? EngineErrorSurface : LoadingSurface,
      { status },
    );
  }
  const normalizedPath = panelPath(activePanel);
  if (
    Spicetify.Platform.History.location.pathname === "/librespot" ||
    Spicetify.Platform.History.location.pathname.startsWith("/librespot/extensions") ||
    Spicetify.Platform.History.location.pathname.startsWith("/librespot/marketplace")
  ) {
    window.setTimeout(() => {
      Spicetify.Platform.History.push(normalizedPath);
    }, 0);
  }
  return h(AppShell, { runtime, snapshot, activePanel });
}
