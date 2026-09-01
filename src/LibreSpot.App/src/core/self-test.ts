export type HealthStatus = "healthy" | "warning" | "broken" | "inactive";
export type RouteState = "wired" | "not-wired" | "unknown";

export type AnchorDefinition = {
  id: string;
  label: string;
  selectors: readonly string[];
  required: boolean;
};

export type HealthCheck = {
  id: string;
  label: string;
  status: HealthStatus;
  detail: string;
  matchedSelector?: string;
  repairAction?: "repair-custom-app-routes";
};

export type HealthReport = {
  healthy: boolean;
  checkedAt: string;
  spotifyVersion: string | null;
  pinnedSpotifyVersion: string;
  checks: HealthCheck[];
};

export const DEFAULT_ANCHORS: readonly AnchorDefinition[] = [
  {
    id: "main-view",
    label: "Main view",
    selectors: [
      ".Root__main-view",
      '[data-testid="main-view-container"]',
      ".main-view-container",
    ],
    required: true,
  },
  {
    id: "navigation",
    label: "Navigation",
    selectors: [
      ".Root__nav-bar",
      '[data-testid="global-nav-bar"]',
      'nav[aria-label="Main"]',
    ],
    required: true,
  },
  {
    id: "playbar",
    label: "Playbar",
    selectors: [
      ".Root__now-playing-bar",
      '[data-testid="now-playing-bar"]',
      ".main-nowPlayingBar-nowPlayingBar",
    ],
    required: true,
  },
  {
    id: "right-sidebar",
    label: "Right sidebar",
    selectors: [
      ".Root__right-sidebar",
      '[data-testid="right-sidebar"]',
      '[aria-label="Now playing view"]',
    ],
    required: false,
  },
  {
    id: "page-scroll",
    label: "Page scroll container",
    selectors: [
      ".main-view-container__scroll-node",
      '[data-testid="main-view-container"] [data-overlayscrollbars-viewport]',
      ".os-viewport",
    ],
    required: true,
  },
] as const;

function checkAnchor(document: Document, anchor: AnchorDefinition): HealthCheck {
  const matchedSelector = anchor.selectors.find((selector) =>
    document.querySelector(selector),
  );
  if (matchedSelector) {
    return {
      id: `anchor:${anchor.id}`,
      label: anchor.label,
      status: "healthy",
      detail: `Matched ${matchedSelector}`,
      matchedSelector,
    };
  }
  return {
    id: `anchor:${anchor.id}`,
    label: anchor.label,
    status: anchor.required ? "broken" : "inactive",
    detail: anchor.required
      ? `None of the supported selectors matched: ${anchor.selectors.join(", ")}`
      : "The region is closed or absent on this page.",
  };
}

function checkRoute(label: string, id: string, state: RouteState): HealthCheck {
  if (state === "wired") {
    return {
      id: `route:${id}`,
      label,
      status: "healthy",
      detail: `/${id} route is wired.`,
    };
  }
  if (state === "not-wired") {
    return {
      id: `route:${id}`,
      label,
      status: "broken",
      detail: `/${id} is installed but its live bundle route is missing. A raw spicetify apply can cause this.`,
      repairAction: "repair-custom-app-routes",
    };
  }
  return {
    id: `route:${id}`,
    label,
    status: "warning",
    detail: "Route wiring could not be confirmed from the live bundle.",
  };
}

export function runSelfTest(options: {
  document: Document;
  spotifyVersion?: string | null;
  pinnedSpotifyVersion?: string;
  librespotRoute?: RouteState;
  marketplaceRoute?: RouteState;
  anchors?: readonly AnchorDefinition[];
  now?: Date;
}): HealthReport {
  const pinnedSpotifyVersion = options.pinnedSpotifyVersion ?? "1.2.93";
  const spotifyVersion = options.spotifyVersion ?? null;
  const checks = (options.anchors ?? DEFAULT_ANCHORS).map((anchor) =>
    checkAnchor(options.document, anchor),
  );
  checks.push(
    checkRoute("LibreSpot route", "librespot", options.librespotRoute ?? "unknown"),
    checkRoute(
      "Marketplace route",
      "marketplace",
      options.marketplaceRoute ?? "unknown",
    ),
  );

  checks.push({
    id: "version:spotify",
    label: "Spotify version",
    status:
      spotifyVersion === null
        ? "warning"
        : spotifyVersion.startsWith(pinnedSpotifyVersion)
          ? "healthy"
          : "warning",
    detail:
      spotifyVersion === null
        ? `Spotify version is unavailable. LibreSpot is pinned to ${pinnedSpotifyVersion}.`
        : spotifyVersion.startsWith(pinnedSpotifyVersion)
          ? `Spotify ${spotifyVersion} matches the ${pinnedSpotifyVersion} pin.`
          : `Spotify ${spotifyVersion} differs from the ${pinnedSpotifyVersion} pin.`,
  });

  return {
    healthy: checks.every(
      (check) => check.status === "healthy" || check.status === "inactive",
    ),
    checkedAt: (options.now ?? new Date()).toISOString(),
    spotifyVersion,
    pinnedSpotifyVersion,
    checks,
  };
}

export class SelfTestMonitor extends EventTarget {
  #report: HealthReport | null = null;

  public constructor(
    private readonly run: () => HealthReport,
  ) {
    super();
  }

  public get report(): HealthReport | null {
    return this.#report;
  }

  public refresh(): HealthReport {
    this.#report = this.run();
    this.dispatchEvent(new CustomEvent<HealthReport>("change", { detail: this.#report }));
    return this.#report;
  }
}
