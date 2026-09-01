import "./app.css";
import { ExtensionsPanel } from "./panels/extensions.ts";
import { FeaturesPanel } from "./panels/features.ts";
import { HealthPanel } from "./panels/health.ts";
import { LookPanel } from "./panels/look.ts";
import { PresetsPanel } from "./panels/presets.ts";
import { TweaksPanel } from "./panels/tweaks.ts";
import type {
  LibreSpotRuntimeApi,
  LibreSpotRuntimeSnapshot,
  UiNode,
} from "./spicetify-globals.d.ts";
import {
  PANEL_DEFINITIONS,
  panelFromPath,
  panelPath,
  type PanelId,
} from "./surface/navigation.ts";
import type {
  PanelComponent,
  PanelProperties,
} from "./surface/panel-types.ts";
import { h } from "./surface/ui.ts";

const PANELS: Record<PanelId, PanelComponent> = {
  look: LookPanel,
  tweaks: TweaksPanel,
  features: FeaturesPanel,
  extensions: ExtensionsPanel,
  presets: PresetsPanel,
  health: HealthPanel,
};

function useRuntime(): LibreSpotRuntimeApi | null {
  const React = Spicetify.React;
  const [runtime, setRuntime] = React.useState<LibreSpotRuntimeApi | null>(
    () => window.LibreSpot ?? null,
  );
  React.useEffect(() => {
    if (runtime) {
      return;
    }
    const timer = window.setInterval(() => {
      if (window.LibreSpot) {
        setRuntime(window.LibreSpot);
        window.clearInterval(timer);
      }
    }, 100);
    return () => {
      window.clearInterval(timer);
    };
  }, [runtime]);
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

function LoadingSurface(): UiNode {
  return h(
    "main",
    {
      className: "librespot-app librespot-loading",
      "data-librespot-app": "loading",
    },
    h("div", { className: "librespot-loading__mark", "aria-hidden": "true" }),
    h("h1", null, "LibreSpot"),
    h("p", null, "Waiting for the live engine to finish loading."),
  );
}

function AppShell(properties: PanelProperties & { activePanel: PanelId }): UiNode {
  const activeDefinition = PANEL_DEFINITIONS.find(
    (panel) => panel.id === properties.activePanel,
  );
  const Panel = PANELS[properties.activePanel];
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
          { className: "librespot-brand__mark", "aria-hidden": "true" },
          h("i"),
          h("i"),
          h("i"),
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
      h(Panel, {
        runtime: properties.runtime,
        snapshot: properties.snapshot,
      }),
    ),
  );
}

export default function LibreSpotApp(): UiNode {
  const runtime = useRuntime();
  const snapshot = useSnapshot(runtime);
  const activePanel = usePanel();
  if (!runtime || !snapshot) {
    return h(LoadingSurface);
  }
  const normalizedPath = panelPath(activePanel);
  if (Spicetify.Platform.History.location.pathname === "/librespot") {
    window.setTimeout(() => {
      Spicetify.Platform.History.push(normalizedPath);
    }, 0);
  }
  return h(AppShell, { runtime, snapshot, activePanel });
}
