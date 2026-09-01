import {
  ManagedRuntimeStyles,
  PALETTE_STYLE_ID,
  SNIPPET_STYLE_ID,
} from "../src/core/index.ts";

describe("managed runtime styles", () => {
  beforeEach(() => {
    document.head.innerHTML = "";
    document.body.innerHTML = "";
    document.documentElement.className = "";
    document.documentElement.removeAttribute("style");
  });

  it("injects one managed palette with hex and rgb variables", () => {
    const runtime = new ManagedRuntimeStyles(document);
    runtime.applyPalette({ main: "010203", text: "fff" });
    runtime.applyPalette({ main: "040506", text: "000" });

    const styles = document.querySelectorAll(`#${PALETTE_STYLE_ID}`);
    expect(styles).toHaveLength(1);
    expect(styles[0]?.textContent).toContain("--spice-main: #040506");
    expect(styles[0]?.textContent).toContain("--spice-rgb-main: 4, 5, 6");
  });

  it("composes layers and keeps exactly one effects tier", () => {
    const runtime = new ManagedRuntimeStyles(document);
    runtime.applyLayers(
      {
        palette: true,
        layout: false,
        effects: true,
        accessibility: true,
      },
      "eco",
    );
    const root = document.documentElement;
    expect(root.classList.contains("librespot-layer-palette")).toBe(true);
    expect(root.classList.contains("librespot-layer-layout")).toBe(false);
    expect(root.classList.contains("librespot-layer-effects")).toBe(true);
    expect(root.classList.contains("librespot-tier-eco")).toBe(true);
    expect(root.classList.contains("librespot-tier-glass")).toBe(false);

    runtime.applyLayers(
      {
        palette: true,
        layout: true,
        effects: false,
        accessibility: false,
      },
      "flat",
    );
    expect(root.classList.contains("librespot-tier-eco")).toBe(false);
    expect(root.classList.contains("librespot-tier-flat")).toBe(true);
  });

  it("writes reviewed snippets through text content", () => {
    const runtime = new ManagedRuntimeStyles(document);
    runtime.applySnippets([".Root__nav-bar { display: none; }", "</style>"]);
    const style = document.getElementById(SNIPPET_STYLE_ID);
    expect(style?.textContent).toContain("</style>");
    expect(document.querySelectorAll(`#${SNIPPET_STYLE_ID}`)).toHaveLength(1);
  });

  it("removes every managed class, variable, and style", () => {
    const runtime = new ManagedRuntimeStyles(document);
    runtime.installLayerStyles();
    runtime.applyPalette({ main: "000" });
    runtime.applySnippets(["body {}"]);
    runtime.setAccent("abc");
    runtime.applyLayers(
      {
        palette: true,
        layout: true,
        effects: true,
        accessibility: true,
      },
      "glass",
    );
    runtime.dispose();

    expect(document.querySelector("[data-librespot-managed]")).toBeNull();
    expect(document.documentElement.className).toBe("");
    expect(
      document.documentElement.style.getPropertyValue("--librespot-accent"),
    ).toBe("");
  });
});
