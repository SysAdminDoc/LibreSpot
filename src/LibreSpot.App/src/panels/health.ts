import circleAlertIcon from "lucide-static/icons/circle-alert.svg";
import circleCheckIcon from "lucide-static/icons/circle-check.svg";
import shieldCheckIcon from "lucide-static/icons/shield-check.svg";
import type { HealthCheck } from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import {
  ActionButton,
  PanelIntro,
  Section,
  h,
} from "../surface/ui.ts";

function statusIcon(status: HealthCheck["status"], className: string): UiNode {
  return h("span", {
    className,
    "aria-hidden": "true",
    dangerouslySetInnerHTML: {
      __html: status === "healthy" ? circleCheckIcon : circleAlertIcon,
    },
  });
}

function checkCard(check: HealthCheck): UiNode {
  return h(
    "div",
    {
      className: `librespot-health-check is-${check.status}`,
      key: check.id,
    },
    statusIcon(check.status, "librespot-health-check__icon"),
    h(
      "span",
      { className: "librespot-health-check__copy" },
      h("strong", null, check.label),
      h("span", null, check.detail),
    ),
    h(
      "span",
      { className: "librespot-health-check__state" },
      check.status === "healthy"
        ? "Active"
        : check.status === "inactive"
          ? "Optional"
          : check.status === "warning"
            ? "Review"
            : "Repair",
    ),
  );
}

function shieldIcon(): UiNode {
  return h("span", {
    className: "librespot-health-privacy__icon",
    "aria-hidden": "true",
    dangerouslySetInnerHTML: { __html: shieldCheckIcon },
  });
}

function checkGroup(properties: {
  title: string;
  description: string;
  checks: readonly HealthCheck[];
}): UiNode {
  return h(
    "section",
    { className: "librespot-health-group", key: properties.title },
    h(
      "div",
      { className: "librespot-health-group__heading" },
      h(
        "span",
        null,
        h("strong", null, properties.title),
        h("span", null, properties.description),
      ),
      h("span", null, `${properties.checks.length} ${properties.checks.length === 1 ? "check" : "checks"}`),
    ),
    h("div", { className: "librespot-health-list" }, ...properties.checks.map(checkCard)),
  );
}

/**
 * Reads a backup from the clipboard. Spotify's client offers no file picker, so
 * the clipboard is the delivery both ways: Back up copies the file out, this
 * reads it back in. Throws with a readable reason so the caller can report it;
 * a silent no-op would look like a dead button.
 */
async function readClipboardBackup(): Promise<string> {
  const clipboard = Spicetify.Platform.ClipboardAPI;
  let pasted: string;
  try {
    pasted =
      typeof clipboard?.paste === "function"
        ? await clipboard.paste()
        : await navigator.clipboard.readText();
  } catch (error) {
    throw new Error(
      `The clipboard could not be read: ${error instanceof Error ? error.message : "permission denied"}.`,
      { cause: error },
    );
  }

  if (pasted.trim().length === 0) {
    throw new Error("The clipboard is empty. Copy a backup file's contents first.");
  }
  return pasted;
}

export function HealthPanel(properties: PanelProperties): UiNode {
  const report = properties.snapshot.health;
  const routeRepairNeeded = report.checks.some(
    (check) => check.repairAction === "repair-custom-app-routes",
  );
  const groups = [
    {
      title: "Live engine",
      description: "Core page anchors are present and responding.",
      checks: report.checks.filter((check) => check.id.startsWith("anchor:")),
    },
    {
      title: "Route wiring",
      description: "LibreSpot and optional app routes are connected.",
      checks: report.checks.filter((check) => check.id.startsWith("route:")),
    },
    {
      title: "Compatibility",
      description: "The current Spotify build is checked against the tested pin.",
      checks: report.checks.filter((check) => check.id.startsWith("version:")),
    },
  ].filter((group) => group.checks.length > 0);

  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "health" },
    PanelIntro({
      eyebrow: "Named checks",
      title: "Health",
      body: report.healthy
        ? `We ran ${report.checks.length} checks across LibreSpot and Spotify. Everything looks good.`
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
      statusIcon(report.healthy ? "healthy" : "warning", "librespot-health-hero__icon"),
      h(
        "div",
        null,
        h("strong", null, report.healthy ? "Everything is working" : "Attention needed"),
        h(
          "span",
          null,
          `Checked ${new Date(report.checkedAt).toLocaleTimeString()} against Spotify ${report.pinnedSpotifyVersion}`,
        ),
      ),
    ),
    h(
      "section",
      { className: "librespot-health-overview", "aria-label": "Health checks" },
      ...groups.map(checkGroup),
      h(
        "footer",
        { className: "librespot-health-privacy" },
        shieldIcon(),
        h("span", null, "Diagnostics contain no account or listening data."),
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
    properties.snapshot.quarantine
      ? Section({
          title: "Recovered state available",
          description: `The saved profile could not be read on ${new Date(properties.snapshot.quarantine.quarantinedAt).toLocaleString()}, so LibreSpot started from defaults and kept the original instead of deleting it. Reason: ${properties.snapshot.quarantine.reason}`,
          children: h(
            "div",
            { className: "librespot-repair-callout" },
            h(
              "p",
              null,
              "Export copies the kept text to the clipboard. Save it before you discard it: it is the only copy of the look you had.",
            ),
            h(
              "div",
              { className: "librespot-health-actions" },
              ActionButton({
                label: "Export recovered state",
                onClick: () => {
                  void properties.runtime.exportQuarantine();
                },
              }),
              ActionButton({
                label: "Discard",
                secondary: true,
                onClick: () => {
                  properties.runtime.discardQuarantine();
                },
              }),
            ),
          ),
        })
      : null,
    Section({
      title: "Back up and restore",
      description:
        "One file holds this profile and the settings Marketplace keeps in its own database, which is what a cleared Spotify profile takes with it. The file stays on this machine.",
      children: h(
        "div",
        { className: "librespot-repair-callout" },
        h(
          "p",
          null,
          "Back up copies the file to the clipboard. Paste it into a text file and keep it somewhere safe. Restore reads that text back from the clipboard.",
        ),
        h(
          "div",
          { className: "librespot-health-actions" },
          ActionButton({
            label: "Back up",
            onClick: () => {
              void properties.runtime.backupState();
            },
          }),
          ActionButton({
            label: "Restore from the clipboard",
            secondary: true,
            onClick: () => {
              void (async () => {
                try {
                  await properties.runtime.restoreState(await readClipboardBackup());
                } catch (error) {
                  properties.runtime.reportError(
                    error instanceof Error ? error.message : "Restore failed.",
                  );
                }
              })();
            },
          }),
        ),
      ),
    }),
  );
}
