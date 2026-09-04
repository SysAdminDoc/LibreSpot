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
  LibreSpotRuntimeApi,
  LibreSpotRuntimeSnapshot,
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

function PanelIcon(properties: { icon: PanelDefinition["icon"] }): UiNode {
  return h("span", {
    className: "librespot-rail__icon",
    "aria-hidden": "true",
    dangerouslySetInnerHTML: { __html: PANEL_ICONS[properties.icon] },
  });
}

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
  const brandIconMask = `url("data:image/svg+xml,${encodeURIComponent(brandIconSource)}")`;
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
