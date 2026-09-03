// Prism - theme.js
// A LibreSpot-original Spicetify theme runtime. Pure UI customization: it sets
// CSS variables and toggles classes on <html>. It never touches audio, ads,
// premium state, telemetry, or any binary. Everything here is reversible by
// removing the theme.
//
// Concepts it demonstrates, each a documented but unbuilt community request:
//   1. Time-scheduled auto light/dark that works despite Spotify's
//      --force-dark-mode (which blinds prefers-color-scheme / matchMedia).
//   2. Per-context accent extracted from the current track's artwork.
//   3. A runtime FPS probe that steps glass effects down on slow machines.
//   4. A single settings menu for all of the above, persisted in localStorage.

(function Prism() {
  const READY = () =>
    window.Spicetify &&
    Spicetify.Player &&
    Spicetify.Platform &&
    Spicetify.LocalStorage &&
    Spicetify.colorExtractor &&
    Spicetify.Menu &&
    document.querySelector(".Root__main-view");

  if (!READY()) {
    setTimeout(Prism, 300);
    return;
  }

  const LS = Spicetify.LocalStorage;
  const KEY = "prism:settings";
  const root = document.documentElement;

  const defaults = {
    mode: "dark", // dark | light | oled | contrast | auto
    autoLightStart: 7, // hour (local) to switch to light
    autoLightEnd: 19, // hour to switch back to dark
    autoLightScheme: "Light",
    autoDarkScheme: "Dark",
    dynamicAccent: true, // pull accent from album art
    glass: "glass", // glass | eco | flat
    autoEco: true, // let the FPS probe downgrade glass
  };

  const load = () => {
    try {
      return Object.assign({}, defaults, JSON.parse(LS.get(KEY) || "{}"));
    } catch {
      return Object.assign({}, defaults);
    }
  };
  let cfg = load();
  const save = () => LS.set(KEY, JSON.stringify(cfg));

  // ---- scheme + class application ------------------------------------
  const SCHEME_CLASS = { glass: "prism-glass", eco: "prism-eco", flat: "prism-flat" };

  function applyScheme(name) {
    // Spicetify exposes scheme switching without a full re-apply.
    try {
      if (Spicetify.Config) Spicetify.Config.color_scheme = name;
      // Marketplace-style: re-read color.ini section is not available here, so
      // we lean on the CLI having written every scheme into colors.css. When a
      // scheme switch needs new vars, ask the config API to set it.
      Spicetify.Platform?.LocalStorageAPI?.setItem?.("prism:scheme", name);
    } catch {}
  }

  function applyClasses() {
    root.classList.remove("prism-glass", "prism-eco", "prism-flat", "prism-contrast");
    if (cfg.mode === "contrast") {
      root.classList.add("prism-contrast", "prism-flat");
    } else {
      root.classList.add(SCHEME_CLASS[cfg.glass] || "prism-glass");
    }
  }

  // ---- 1. time-scheduled auto light/dark -----------------------------
  function resolvedMode() {
    if (cfg.mode !== "auto") return cfg.mode;
    const h = new Date().getHours();
    const day = cfg.autoLightStart <= cfg.autoLightEnd
      ? h >= cfg.autoLightStart && h < cfg.autoLightEnd
      : h >= cfg.autoLightStart || h < cfg.autoLightEnd;
    return day ? "light" : "dark";
  }

  function tick() {
    const mode = resolvedMode();
    const scheme =
      mode === "light" ? cfg.autoLightScheme :
      mode === "oled" ? "OLED" :
      mode === "contrast" ? "HighContrast" :
      cfg.autoDarkScheme;
    applyScheme(scheme);
    applyClasses();
  }

  // ---- 2. per-context accent from artwork ----------------------------
  async function refreshAccent() {
    if (!cfg.dynamicAccent || cfg.mode === "contrast") {
      root.style.removeProperty("--prism-accent");
      return;
    }
    const uri = Spicetify.Player.data?.item?.uri;
    if (!uri) return;
    try {
      const colors = await Spicetify.colorExtractor(uri);
      const pick = colors?.VIBRANT || colors?.LIGHT_VIBRANT || colors?.PROMINENT;
      if (pick) root.style.setProperty("--prism-accent", pick);
    } catch {
      // artwork colour service can 403; keep the scheme accent silently.
    }
  }

  // ---- 3. FPS probe -> eco downgrade ---------------------------------
  function probeFps(done) {
    let frames = 0;
    const start = performance.now();
    (function count() {
      frames++;
      if (performance.now() - start < 1000) requestAnimationFrame(count);
      else done(frames);
    })();
  }

  function maybeDowngrade() {
    if (!cfg.autoEco || cfg.glass !== "glass") return;
    probeFps((fps) => {
      if (fps < 45) {
        cfg.glass = "eco";
        save();
        applyClasses();
        Spicetify.showNotification?.("Prism: reduced glass effects for smoother playback");
      }
    });
  }

  // ---- 4. settings menu ----------------------------------------------
  function openSettings() {
    const modes = ["dark", "light", "oled", "contrast", "auto"];
    const glass = ["glass", "eco", "flat"];
    const cycle = (arr, cur) => arr[(arr.indexOf(cur) + 1) % arr.length];
    const container = document.createElement("div");
    container.innerHTML = `
      <div style="display:flex;flex-direction:column;gap:14px;min-width:320px">
        <label style="display:flex;justify-content:space-between;align-items:center">
          Appearance
          <button id="prism-mode" class="prism-btn">${cfg.mode}</button>
        </label>
        <label style="display:flex;justify-content:space-between;align-items:center">
          Effects
          <button id="prism-glass" class="prism-btn">${cfg.glass}</button>
        </label>
        <label style="display:flex;justify-content:space-between;align-items:center">
          Accent from album art
          <button id="prism-accent" class="prism-btn">${cfg.dynamicAccent ? "on" : "off"}</button>
        </label>
        <label style="display:flex;justify-content:space-between;align-items:center">
          Auto-reduce effects when slow
          <button id="prism-eco" class="prism-btn">${cfg.autoEco ? "on" : "off"}</button>
        </label>
        <p style="opacity:.7;font-size:12px;margin:0">
          Auto appearance switches to <b>${cfg.autoLightScheme}</b> from
          ${cfg.autoLightStart}:00 to ${cfg.autoLightEnd}:00, otherwise
          <b>${cfg.autoDarkScheme}</b>.
        </p>
      </div>
      <style>
        .prism-btn{background:var(--spice-button);color:var(--spice-main);
          border:0;border-radius:6px;padding:6px 14px;cursor:pointer;
          text-transform:capitalize;min-width:80px}
      </style>`;
    container.querySelector("#prism-mode").onclick = (e) => {
      cfg.mode = cycle(modes, cfg.mode); e.target.textContent = cfg.mode; save(); tick(); refreshAccent();
    };
    container.querySelector("#prism-glass").onclick = (e) => {
      cfg.glass = cycle(glass, cfg.glass); e.target.textContent = cfg.glass; save(); applyClasses();
    };
    container.querySelector("#prism-accent").onclick = (e) => {
      cfg.dynamicAccent = !cfg.dynamicAccent; e.target.textContent = cfg.dynamicAccent ? "on" : "off"; save(); refreshAccent();
    };
    container.querySelector("#prism-eco").onclick = (e) => {
      cfg.autoEco = !cfg.autoEco; e.target.textContent = cfg.autoEco ? "on" : "off"; save();
    };
    Spicetify.PopupModal.display({ title: "Prism", content: container, isLarge: true });
  }

  new Spicetify.Menu.Item("Prism settings", false, openSettings).register();

  // ---- wire up --------------------------------------------------------
  tick();
  refreshAccent();
  maybeDowngrade();
  Spicetify.Player.addEventListener("songchange", refreshAccent);
  setInterval(tick, 60 * 1000); // re-check the schedule every minute

  console.log("[Prism] ready");
})();
