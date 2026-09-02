import type { HealthCheck } from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import {
  ActionButton,
  PanelIntro,
  Section,
  SpotifyIcon,
  h,
} from "../surface/ui.ts";

function checkCard(check: HealthCheck): UiNode {
  return h(
    "article",
    {
      className: `librespot-health-check is-${check.status}`,
      key: check.id,
    },
    h(
      "div",
      { className: "librespot-health-check__title" },
      h("span", {
        className: `librespot-health-dot is-${check.status}`,
        "aria-hidden": "true",
      }),
      h("strong", null, check.label),
      h("span", { className: "librespot-badge" }, check.status),
    ),
    h("p", null, check.detail),
  );
}

export function HealthPanel(properties: PanelProperties): UiNode {
  const report = properties.snapshot.health;
  const visibleChecks = report.healthy
    ? report.checks.filter((check) => check.status !== "healthy")
    : report.checks.filter(
        (check) => check.status === "broken" || check.status === "warning",
      );
  const routeRepairNeeded = report.checks.some(
    (check) => check.repairAction === "repair-custom-app-routes",
  );

  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "health" },
    PanelIntro({
      eyebrow: "Named checks",
      title: "Health",
      body: report.healthy
        ? "The engine's required anchors, routes, and Spotify pin are healthy. Closed optional regions stay quiet."
        : "A required anchor, route, or version check needs attention. Each warning names the dependency that changed.",
      action: h(
        "div",
        { className: "librespot-health-actions" },
        ActionButton({
          label: "Check again",
          secondary: true,
          onClick: () => {
            properties.runtime.refreshHealth();
          },
        }),
        ActionButton({
          label: "Copy diagnostics",
          onClick: () => {
            void properties.runtime.copyDiagnostics();
          },
        }),
      ),
    }),
    h(
      "div",
      {
        className: report.healthy
          ? "librespot-health-hero is-healthy"
          : "librespot-health-hero is-warning",
      },
      h(
        "span",
        { className: "librespot-health-hero__icon", "aria-hidden": "true" },
        SpotifyIcon({
          name: report.healthy ? "check" : "exclamation-circle",
        }),
      ),
      h(
        "div",
        null,
        h("strong", null, report.healthy ? "Engine ready" : "Attention needed"),
        h(
          "span",
          null,
          `Checked ${new Date(report.checkedAt).toLocaleTimeString()} against Spotify ${report.pinnedSpotifyVersion}`,
        ),
      ),
    ),
    routeRepairNeeded
      ? Section({
          title: "Custom-app route repair",
          description: "A raw spicetify apply removed route wiring. LibreSpot Desktop can repair the live bundle without replacing your profile.",
          children: h(
            "div",
            { className: "librespot-repair-callout" },
            h(
              "p",
              null,
              "Copy the current profile, open LibreSpot Desktop, import it, then run the named custom-app route repair.",
            ),
            ActionButton({
              label: "Copy repair handoff",
              onClick: () => {
                void properties.runtime.copyProfile();
              },
            }),
          ),
        })
      : null,
    visibleChecks.length > 0
      ? Section({
          title: "Needs attention",
          children: h(
            "div",
            { className: "librespot-health-list" },
            ...visibleChecks.map(checkCard),
          ),
        })
      : null,
    h(
      "details",
      { className: "librespot-health-details" },
      h("summary", null, `All ${report.checks.length} checks`),
      h(
        "div",
        { className: "librespot-health-list" },
        ...report.checks.map(checkCard),
      ),
    ),
  );
}
