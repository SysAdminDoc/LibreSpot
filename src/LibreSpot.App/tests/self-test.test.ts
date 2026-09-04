import {
  DEFAULT_ANCHORS,
  runSelfTest,
  type AnchorDefinition,
} from "../src/core/index.ts";

function loadHealthyFixture(): void {
  document.body.innerHTML = `
    <nav data-testid="global-nav-bar"></nav>
    <main data-testid="main-view-container">
      <div data-overlayscrollbars-viewport></div>
    </main>
    <footer data-testid="now-playing-bar"></footer>
  `;
}

describe("engine self-test", () => {
  it("is quiet when stable anchors, routes, and the pin are healthy", () => {
    loadHealthyFixture();
    const report = runSelfTest({
      document,
      spotifyVersion: "1.2.93.667.g7b5cc0ce",
      librespotRoute: "wired",
      marketplaceRoute: "wired",
      now: new Date("2026-09-01T12:00:00Z"),
    });
    expect(report.healthy).toBe(true);
    expect(report.checks.filter((check) => check.status === "broken")).toEqual([]);
  });

  it("names the missing anchor instead of failing silently", () => {
    loadHealthyFixture();
    document.querySelector('[data-testid="global-nav-bar"]')?.remove();
    const report = runSelfTest({
      document,
      spotifyVersion: "1.2.93.667.g7b5cc0ce",
      librespotRoute: "wired",
      marketplaceRoute: "wired",
    });
    expect(report.healthy).toBe(false);
    expect(report.checks).toContainEqual(
      expect.objectContaining({
        id: "anchor:navigation",
        label: "Navigation",
        status: "broken",
      }),
    );
  });

  it("offers route repair when raw apply removed custom-app wiring", () => {
    loadHealthyFixture();
    const report = runSelfTest({
      document,
      spotifyVersion: "1.2.93",
      librespotRoute: "not-wired",
      marketplaceRoute: "not-wired",
    });
    expect(report.checks).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          id: "route:marketplace",
          repairAction: "repair-custom-app-routes",
          status: "broken",
        }),
      ]),
    );
  });

  it("keeps an optional Marketplace route quiet when it is not selected", () => {
    loadHealthyFixture();
    const report = runSelfTest({
      document,
      spotifyVersion: "1.2.93",
      librespotRoute: "wired",
      marketplaceRoute: "inactive",
    });

    expect(report.healthy).toBe(true);
    expect(report.checks).toContainEqual(
      expect.objectContaining({
        id: "route:marketplace",
        status: "inactive",
      }),
    );
  });

  it("accepts a changed selector through the declared anchor contract", () => {
    document.body.innerHTML = '<main id="future-main"></main>';
    const anchors: AnchorDefinition[] = [
      {
        id: "main-view",
        label: "Main view",
        selectors: ["#future-main"],
        required: true,
      },
    ];
    const report = runSelfTest({
      document,
      anchors,
      librespotRoute: "wired",
      marketplaceRoute: "wired",
      spotifyVersion: "1.2.93",
    });
    expect(report.checks[0]).toEqual(
      expect.objectContaining({ status: "healthy", matchedSelector: "#future-main" }),
    );
    expect(DEFAULT_ANCHORS.length).toBeGreaterThan(3);
  });
});
