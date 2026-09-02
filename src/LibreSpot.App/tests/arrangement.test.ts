import {
  applyHomeArrangement,
  applySidebarArrangement,
} from "../src/core/index.ts";

describe("live Spotify arrangement", () => {
  beforeEach(() => {
    document.body.innerHTML = "";
  });

  it("reorders Home sections by stable identity and restores natural order", () => {
    document.body.innerHTML = `
      <main class="main-home-content">
        <section aria-label="Alpha shelf"><h2>Alpha</h2></section>
        <section aria-label="Beta shelf"><h2>Beta</h2></section>
      </main>
    `;

    const moved = applyHomeArrangement(document, ["Beta shelf", "Alpha shelf"]);
    expect(moved.items.map((item) => item.label)).toEqual(["Beta", "Alpha"]);
    expect(moved.applied).toBe(true);
    expect(
      [...document.querySelectorAll(".main-home-content > section")].map(
        (section) => section.getAttribute("aria-label"),
      ),
    ).toEqual(["Beta shelf", "Alpha shelf"]);

    const restored = applyHomeArrangement(document, []);
    expect(restored.items.map((item) => item.label)).toEqual(["Alpha", "Beta"]);
    expect(restored.applied).toBe(true);
  });

  it("reorders visible Library rows and keeps row indices accurate", () => {
    document.body.innerHTML = `
      <aside class="Root__nav-bar">
        <div role="grid" aria-label="Your Library">
          <div role="presentation">
            <div role="row" aria-selected="false" aria-rowindex="1">
              <div role="group" aria-labelledby="listrow-title-spotify:playlist:a">
                <span id="listrow-title-spotify:playlist:a">Alpha playlist</span>
              </div>
            </div>
            <div role="row" aria-selected="false" aria-rowindex="2">
              <div role="group" aria-labelledby="listrow-title-spotify:playlist:b">
                <span id="listrow-title-spotify:playlist:b">Beta playlist</span>
              </div>
            </div>
          </div>
        </div>
      </aside>
    `;

    const moved = applySidebarArrangement(document, [
      "spotify:playlist:b",
      "spotify:playlist:a",
    ]);
    expect(moved.items.map((item) => item.label)).toEqual([
      "Beta playlist",
      "Alpha playlist",
    ]);
    expect(
      [...document.querySelectorAll('[role="row"]')].map((row) =>
        row.getAttribute("aria-rowindex"),
      ),
    ).toEqual(["1", "2"]);

    const restored = applySidebarArrangement(document, []);
    expect(restored.items.map((item) => item.label)).toEqual([
      "Alpha playlist",
      "Beta playlist",
    ]);
  });
});
