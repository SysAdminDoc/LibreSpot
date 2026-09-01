Warning: truncated output (original token count: 162970)
Total output lines: 11784

"use strict";
(() => {
  // src/glassSurface.ts
  var SVG_NS = "http://www.w3.org/2000/svg";
  var DEFAULTS = {
    borderRadius: 20,
    borderWidth: 0.07,
    brightness: 50,
    opacity: 0.93,
    blur: 2,
    glassBlur: "",
    backdropBlur: "",
    displace: 0.2,
    saturation: 1,
    distortionScale: -80,
    chromaticAberration: true,
    redOffset: 0,
    greenOffset: 6,
    blueOffset: 10,
    xChannel: "R",
    yChannel: "G",
    mixBlendMode: "screen",
    applyTo: "element"
  };
  var instanceCounter = 0;
  var filterDefs = null;
  var svgFilterSupport = null;
  function supportsSVGFilters() {
    if (svgFilterSupport !== null) return svgFilterSupport;
    const ua = navigator.userAgent;
    const isWebkit = /Safari/.test(ua) && !/Chrome/.test(ua);
    const isFirefox = /Firefox/.test(ua);
    if (isWebkit || isFirefox) {
      svgFilterSupport = false;
    } else {
      const div = document.createElement("div");
      div.style.backdropFilter = "url(#liquify-probe)";
      svgFilterSupport = div.style.backdropFilter !== "";
    }
    return svgFilterSupport;
  }
  function ensureFilterDefs() {
    if (filterDefs && filterDefs.isConnected) return filterDefs;
    const host = document.createElementNS(SVG_NS, "svg");
    host.setAttribute("id", "liquify-filter-host");
    host.setAttribute("width", "0");
    host.setAttribute("height", "0");
    host.setAttribute("aria-hidden", "true");
    filterDefs = document.createElementNS(SVG_NS, "defs");
    host.appendChild(filterDefs);
    document.body.appendChild(host);
    return filterDefs;
  }
  var GLASS_STORAGE_KEY = "liquify-glass-enabled";
  var SIMPLE_STYLE_ID = "liquify-glass-simple-style";
  var BULK_FILTER_ID = "glass-filter--r1-7";
  var BULK_FILTER_HOST_ID = "liquify-bulk-filter-host";
  var BULK_STYLE_ID = "liquify-glass-bulk-style";
  var PERF_CLASS = "liquify-perf-no-glass";
  var instances = /* @__PURE__ */ new Set();
  var glassEnabled = readGlassEnabled();
  function readGlassEnabled() {
    try {
      return localStorage.getItem(GLASS_STORAGE_KEY) !== "off";
    } catch {
      return true;
    }
  }
  function ensureSimpleStyle() {
    if (document.getElementById(SIMPLE_STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = SIMPLE_STYLE_ID;
    style.textContent = ".liquify-glass--simple{background:transparent;backdrop-filter:blur(var(--liquify-backdrop-blur, 2rem)) saturate(1.4);-webkit-backdrop-filter:blur(var(--liquify-backdrop-blur, 2rem)) saturate(1.4);}";
    document.head.appendChild(style);
  }
  function isGlassEnabled() {
    return glassEnabled;
  }
  function setGlassEnabled(enabled) {
    glassEnabled = enabled;
    try {
      localStorage.setItem(GLASS_STORAGE_KEY, enabled ? "on" : "off");
    } catch {
    }
    for (const instance of instances) instance.refreshMode();
    document.documentElement.classList.toggle(PERF_CLASS, !enabled);
  }
  function bulkDisplacementMap() {
    const w2 = 400;
    const h2 = 200;
    const r = 20;
    const edge = Math.min(w2, h2) * (0.07 * 0.5);
    const svg2 = `<svg viewBox="0 0 ${w2} ${h2}" xmlns="http://www.w3.org/2000/svg"><defs><linearGradient id="lqbulk-rg" x1="100%" y1="0%" x2="0%" y2="0%"><stop offset="0%" stop-color="#0000"/><stop offset="100%" stop-color="red"/></linearGradient><linearGradient id="lqbulk-bg" x1="0%" y1="0%" x2="0%" y2="100%"><stop offset="0%" stop-color="#0000"/><stop offset="100%" stop-color="blue"/></linearGradient></defs><rect x="0" y="0" width="${w2}" height="${h2}" fill="black"></rect><rect x="0" y="0" width="${w2}" height="${h2}" rx="${r}" fill="url(#lqbulk-rg)" /><rect x="0" y="0" width="${w2}" height="${h2}" rx="${r}" fill="url(#lqbulk-bg)" style="mix-blend-mode: screen" /><rect x="${edge}" y="${edge}" width="${w2 - edge * 2}" height="${h2 - edge * 2}" rx="${r}" fill="hsl(0 0% 50% / 0.93)" style="filter:blur(2px)" /></svg>`;
    return `data:image/svg+xml,${encodeURIComponent(svg2)}`;
  }
  function ensureSharedGlassFilter() {
    if (!supportsSVGFilters()) return;
    if (document.getElementById(BULK_FILTER_HOST_ID)) return;
    const fe = (name, attrs) => {
      const node = document.createElementNS(SVG_NS, name);
      for (const [k, v2] of Object.entries(attrs)) node.setAttribute(k, v2);
      return node;
    };
    const host = document.createElementNS(SVG_NS, "svg");
    host.setAttribute("id", BULK_FILTER_HOST_ID);
    host.setAttribute("width", "0");
    host.setAttribute("height", "0");
    host.setAttribute("aria-hidden", "true");
    const defs = document.createElementNS(SVG_NS, "defs");
    const filter = fe("filter", {
      id: BULK_FILTER_ID,
      "color-interpolation-filters": "sRGB",
      x: "0%",
      y: "0%",
      width: "100%",
      height: "100%"
    });
    const feImage = fe("feImage", { x: "0", y: "0", width: "100%", height: "100%", preserveAspectRatio: "none", result: "map" });
    const map = bulkDisplacementMap();
    feImage.setAttribute("href", map);
    feImage.setAttributeNS("http://www.w3.org/1999/xlink", "xlink:href", map);
    filter.appendChild(feImage);
    const distortionScale = -80;
    const channels = [
      { name: "Red", offset: 0, matrix: "1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0" },
      { name: "Green", offset: 6, matrix: "0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0" },
      { name: "Blue", offset: 10, matrix: "0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0" }
    ];
    for (const c2 of channels) {
      filter.appendChild(fe("feDisplacementMap", { in: "SourceGraphic", in2: "map", scale: String(distortionScale + c2.offset), xChannelSelector: "R", yChannelSelector: "G", result: `disp${c2.name}` }));
      filter.appendChild(fe("feColorMatrix", { in: `disp${c2.name}`, type: "matrix", values: c2.matrix, result: c2.name.toLowerCase() }));
    }
    filter.appendChild(fe("feBlend", { in: "red", in2: "green", mode: "screen", result: "rg" }));
    filter.appendChild(fe("feBlend", { in: "rg", in2: "blue", mode: "screen", result: "output" }));
    filter.appendChild(fe("feGaussianBlur", { in: "output", stdDeviation: "0.2" }));
    defs.appendChild(filter);
    host.appendChild(defs);
    document.body.appendChild(host);
  }
  function applyBulkGlass(targets) {
    ensureSharedGlassFilter();
    const defaultSelectors = [];
    const extraRules = [];
    const allSelectors = [];
    const perfOverrides = [];
    for (const t of targets) {
      const bright = t.brightness != null ? ` brightness(${t.brightness})` : "";
      if (t.before) {
        const b2 = t.blur != null ? `${t.blur}px` : "var(--liquify-glass-blur, 2px)";
        extraRules.push(
          `${t.selector}::before{content:"";position:absolute;inset:0;border-radius:inherit;z-index:-1;pointer-events:none;backdrop-filter:var(--glass-filter) blur(${b2})${bright};-webkit-backdrop-filter:var(--glass-filter) blur(${b2})${bright};}`
        );
      } else if (t.blur != null || bright) {
        const b2 = t.blur != null ? `${t.blur}px` : "var(--liquify-glass-blur, 2px)";
        extraRules.push(
          `${t.selector}{backdrop-filter:var(--glass-filter) blur(${b2})${bright};-webkit-backdrop-filter:var(--glass-filter) blur(${b2})${bright};}`
        );
        allSelectors.push(t.selector);
        if (bright) perfOverrides.push({ selector: t.selector, bright });
      } else {
        defaultSelectors.push(t.selector);
        allSelectors.push(t.selector);
      }
    }
    const perfBlur = "blur(var(--liquify-backdrop-blur, 2rem)) saturate(1.4)";
    const css = `:root{--glass-filter:url(#${BULK_FILTER_ID});}` + (defaultSelectors.length ? `${defaultSelectors.join(",")}{backdrop-filter:var(--glass-filter) blur(var(--liquify-glass-blur, 2px));-webkit-backdrop-filter:var(--glass-filter) blur(var(--liquify-glass-blur, 2px));}` : "") + extraRules.join("") + (allSelectors.length ? `html.${PERF_CLASS} :is(${allSelectors.join(",")}){backdrop-filter:${perfBlur};-webkit-backdrop-filter:${perfBlur};}` : "") + // After the blanket perf rule, so these keep their darkening in perf mode.
    perfOverrides.map(
      ({ selector, bright }) => `html.${PERF_CLASS} ${selector}{backdrop-filter:${perfBlur}${bright};-webkit-backdrop-filter:${perfBlur}${bright};}`
    ).join("");
    let style = document.getElementById(BULK_STYLE_ID);
    if (!style) {
      style = document.createElement("style");
      style.id = BULK_STYLE_ID;
      document.head.appendChild(style);
    }
    style.textContent = css;
    document.documentElement.classList.toggle(PERF_CLASS, !glassEnabled);
  }
  function installGlassDevtools() {
    ensureSimpleStyle();
    window.liquifyGlass = {
      enable: () => setGlassEnabled(true),
      disable: () => setGlassEnabled(false),
      toggle: () => setGlassEnabled(!glassEnabled),
      get enabled() {
        return glassEnabled;
      }
    };
  }
  var GlassSurface = class {
    constructor(el, options = {}) {
      this.filter = null;
      this.feImage = null;
      this.resizeObserver = null;
      this.attrObserver = null;
      this.styleEl = null;
      this.destroyed = false;
      this.lastSize = { width: 0, height: 0 };
      this.syncScheduled = false;
      this.scheduleSync = () => {
        if (this.syncScheduled || this.destroyed) return;
        this.syncScheduled = true;
        requestAnimationFrame(() => {
          this.syncScheduled = false;
          this.updateDisplacementMap();
        });
      };
      this.el = el;
      this.opts = { ...DEFAULTS, ...options };
      this.filterId = `liquify-filter-${++instanceCounter}`;
      instances.add(this);
      if (supportsSVGFilters()) {
        this.buildFilter();
        this.applyStyles();
        this.updateDisplacementMap();
        this.resizeObserver = new ResizeObserver(this.scheduleSync);
        this.resizeObserver.observe(el);
      } else {
        this.applyStyles();
      }
      this.attrObserver = new MutationObserver(() => this.ensureApplied());
      this.attrObserver.observe(el, { attributes: true, attributeFilter: ["class", "style"] });
    }
    /**
     * Re-applies class and marker attribute in case Spotify wiped them.
     * Called on every mutation batch, so it must stay cheap: no layout reads —
     * geometry changes are the ResizeObserver's job.
     */
    ensureApplied() {
      if (this.destroyed) return;
      if (!this.el.classList.contains("liquify-glass") || this.el.getAttribute("data-liquify") !== this.filterId) {
        this.applyStyles();
      }
    }
    destroy() {
      if (this.destroyed) return;
      this.destroyed = true;
      instances.delete(this);
      this.resizeObserver?.disconnect();
      this.resizeObserver = null;
      this.attrObserver?.disconnect();
      this.attrObserver = null;
      this.filter?.remove();
      this.filter = null;
      this.feImage = null;
      this.styleEl?.remove();
      this.styleEl = null;
      this.el.classList.remove("liquify-glass", "liquify-glass--svg", "liquify-glass--simple", "liquify-glass--before");
      this.el.removeAttribute("data-liquify");
    }
    /** Re-applies the current on/off mode (called by the DevTools toggle). */
    refreshMode() {
      if (this.destroyed) return;
      this.applyStyles();
    }
    applyStyles() {
      const useSvg = glassEnabled && supportsSVGFilters();
      this.el.classList.add("liquify-glass");
      this.el.classList.toggle("liquify-glass--svg", useSvg);
      this.el.classList.toggle("liquify-glass--simple", !useSvg);
      this.el.classList.toggle("liquify-glass--before", this.opts.applyTo === "before");
      this.el.setAttribute("data-liquify", this.filterId);
      this.ensureInstanceStyle();
    }
    /**
     * Element-static custom properties in an own stylesheet rule (Spotify rewrites
     * the inline style attribute, which would wipe them). Holds this element's
     * displacement filter and any per-element blur overrides — none of which
     * depend on the on/off mode, so the rule is built once.
     *
     * A per-element blur is written as `var(--…-special, <value>)` so theme
     * settings can override every special element at once via that variable,
     * while unset elements keep their own value.
     */
    ensureInstanceStyle() {
      if (this.styleEl) return;
      const decls = [];
      if (supportsSVGFilters()) {
        const saturate = this.opts.saturation !== 1 ? ` saturate(${this.opts.saturation})` : "";
        decls.push(`--liquify-filter:url(#${this.filterId})${saturate}`);
      }
      if (this.opts.glassBlur) {
        decls.push(`--liquify-glass-blur:${this.opts.glassBlur}`);
      }
      if (this.opts.backdropBlur) {
        decls.push(`--liquify-backdrop-blur:var(--liquify-backdrop-blur-special, ${this.opts.backdropBlur})`);
      }
      if (decls.length === 0) return;
      this.styleEl = document.createElement("style");
      this.styleEl.textContent = `[data-liquify="${this.filterId}"]{${decls.join(";")};}`;
      document.head.appendChild(this.styleEl);
    }
    /** Builds the SVG filter chain (per-channel displacement + screen blend). */
    buildFilter() {
      const fe = (name, attrs) => {
        const node = document.createElementNS(SVG_NS, name);
        for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
        return node;
      };
      const o = this.opts;
      const filter = fe("filter", {
        id: this.filterId,
        "color-interpolation-filters": "sRGB",
        x: "0%",
        y: "0%",
        width: "100%",
        height: "100%"
      });
      this.feImage = fe("feImage", {
        x: "0",
        y: "0",
        width: "100%",
        height: "100%",
        preserveAspectRatio: "none",
        result: "map"
      });
      filter.appendChild(this.feImage);
      if (o.chromaticAberration) {
        const channels = [
          {
            name: "Red",
            offset: o.redOffset,
            matrix: "1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0"
          },
          {
            name: "Green",
            offset: o.greenOffset,
            matrix: "0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0"
          },
          {
            name: "Blue",
            offset: o.blueOffset,
            matrix: "0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0"
          }
        ];
        for (const channel of channels) {
          filter.appendChild(
            fe("feDisplacementMap", {
              in: "SourceGraphic",
              in2: "map",
              scale: String(o.distortionScale + channel.offset),
              xChannelSelector: o.xChannel,
              yChannelSelector: o.yChannel,
              result: `disp${channel.name}`
            })
          );
          filter.appendChild(
            fe("feColorMatrix", {
              in: `disp${channel.name}`,
              type: "matrix",
              values: channel.matrix,
              result: channel.name.toLowerCase()
            })
          );
        }
        filter.appendChild(fe("feBlend", { in: "red", in2: "green", mode: "screen", result: "rg" }));
        filter.appendChild(fe("feBlend", { in: "rg", in2: "blue", mode: "screen", result: "output" }));
      } else {
        filter.appendChild(
          fe("feDisplacementMap", {
            in: "SourceGraphic",
            in2: "map",
            scale: String(o.distortionScale),
            xChannelSelector: o.xChannel,
            yChannelSelector: o.yChannel,
            result: "output"
          })
        );
      }
      filter.appendChild(fe("feGaussianBlur", { in: "output", stdDeviation: String(o.displace) }));
      ensureFilterDefs().appendChild(filter);
      this.filter = filter;
    }
    /**
     * The reactbits original displacement map: red ramps transparent→red
     * right-to-left (x axis), blue ramps transparent→blue top-to-bottom and is
     * screen-blended on top. The blurred inner rect keeps the centre clear, so
     * only the edge band refracts.
     */
    generateDisplacementMap(actualWidth, actualHeight) {
      const o = this.opts;
      const edgeSize = Math.min(actualWidth, actualHeight) * (o.borderWidth * 0.5);
      const redGradId = `${this.filterId}-red-grad`;
      const blueGradId = `${this.filterId}-blue-grad`;
      const svgContent = `
      <svg viewBox="0 0 ${actualWidth} ${actualHeight}" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <linearGradient id="${redGradId}" x1="100%" y1="0%" x2="0%" y2="0%">
            <stop offset="0%" stop-color="#0000"/>
            <stop offset="100%" stop-color="red"/>
          </linearGradient>
          <linearGradient id="${blueGradId}" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stop-color="#0000"/>
            <stop offset="100%" stop-color="blue"/>
          </linearGradient>
        </defs>
        <rect x="0" y="0" width="${actualWidth}" height="${actualHeight}" fill="black"></rect>
        <rect x="0" y="0" width="${actualWidth}" height="${actualHeight}" rx="${o.borderRadius}" fill="url(#${redGradId})" />
        <rect x="0" y="0" width="${actualWidth}" height="${actualHeight}" rx="${o.borderRadius}" fill="url(#${blueGradId})" style="mix-blend-mode: ${o.mixBlendMode}" />
        <rect x="${edgeSize}" y="${edgeSize}" width="${actualWidth - edgeSize * 2}" height="${actualHeight - edgeSize * 2}" rx="${o.borderRadius}" fill="hsl(0 0% ${o.brightness}% / ${o.opacity})" style="filter:blur(${o.blur}px)" />
      </svg>
    `;
      return `data:image/svg+xml,${encodeURIComponent(svgContent)}`;
    }
    updateDisplacementMap() {
      if (this.destroyed || !this.feImage) return;
      const rect = this.el.getBoundingClientRect();
      if (!rect.width || !rect.height) return;
      if (rect.width === this.lastSize.width && rect.height === this.lastSize.height) return;
      this.lastSize = { width: rect.width, height: rect.height };
      const dataUrl = this.generateDisplacementMap(rect.width, rect.height);
      this.feImage.setAttribute("href", dataUrl);
      this.feImage.setAttributeNS("http://www.w3.org/1999/xlink", "xlink:href", dataUrl);
      this.feImage.setAttribute("width", "100%");
      this.feImage.setAttribute("height", "100%");
    }
  };

  // src/observer.ts
  function watchGlassTargets(targets) {
    const attached = /* @__PURE__ */ new Map();
    let scheduled = false;
    const scan = () => {
      scheduled = false;
      const matched = /* @__PURE__ */ new Set();
      for (const target of targets) {
        for (const el of document.querySelectorAll(target.selector)) {
          if (el.closest(".liquify-popup-clone")) continue;
          matched.add(el);
          const existing = attached.get(el);
          if (existing) {
            existing.ensureApplied();
          } else {
            attached.set(el, new GlassSurface(el, target.options));
          }
        }
      }
      for (const [el, surface] of attached) {
        if (!el.isConnected || !matched.has(el)) {
          surface.destroy();
          attached.delete(el);
        }
      }
    };
    const schedule = () => {
      if (scheduled) return;
      scheduled = true;
      requestAnimationFrame(scan);
    };
    const observer = new MutationObserver(schedule);
    observer.observe(document.body, {
      childList: true,
      subtree: true
    });
    scan();
    return () => {
      observer.disconnect();
      for (const surface of attached.values()) surface.destroy();
      attached.clear();
    };
  }

  // node_modules/@kawarp/core/dist/index.js
  var BLUR_SIZE = 128;
  var VERTEX_SHADER = `
  attribute vec2 a_position;
  attribute vec2 a_texCoord;
  varying vec2 v_texCoord;
  void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_texCoord = a_texCoord;
  }
`;
  var KAWASE_BLUR_SHADER = `
  precision highp float;
  uniform sampler2D u_texture;
  uniform vec2 u_resolution;
  uniform float u_offset;
  varying vec2 v_texCoord;

  void main() {
    highp vec2 texelSize = 1.0 / u_resolution;
    highp vec4 color = vec4(0.0);

    color += texture2D(u_texture, v_texCoord + vec2(-u_offset, -u_offset) * texelSize);
    color += texture2D(u_texture, v_texCoord + vec2(u_offset, -u_offset) * texelSize);
    color += texture2D(u_texture, v_texCoord + vec2(-u_offset, u_offset) * texelSize);
    color += texture2D(u_texture, v_texCoord + vec2(u_offset, u_offset) * texelSize);

    gl_FragColor = color * 0.25;
  }
`;
  var BLEND_SHADER = `
  precision highp float;
  uniform sampler2D u_texture1;
  uniform sampler2D u_texture2;
  uniform float u_blend;
  varying vec2 v_texCoord;

  void main() {
    vec4 color1 = texture2D(u_texture1, v_texCoord);
    vec4 color2 = texture2D(u_texture2, v_texCoord);
    gl_FragColor = mix(color1, color2, u_blend);
  }
`;
  var TINT_SHADER = `
  precision highp float;
  uniform sampler2D u_texture;
  uniform vec3 u_tintColor;
  uniform float u_tintIntensity;
  varying vec2 v_texCoord;

  void main() {
    vec4 color = texture2D(u_texture, v_texCoord);
    float luma = dot(color.rgb, vec3(0.299, 0.587, 0.114));

    // darkMask: 1.0 for black, 0.0 for luma >= 0.5
    float darkMask = 1.0 - smoothstep(0.0, 0.5, luma);

    // Blend dark areas toward tint color
    color.rgb = mix(color.rgb, u_tintColor, darkMask * u_tintIntensity);

    gl_FragColor = color;
  }
`;
  var DOMAIN_WARP_SHADER = `
  precision highp float;
  uniform sampler2D u_texture;
  uniform float u_time;
  uniform float u_intensity;
  varying vec2 v_texCoord;

  vec3 mod289(vec3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
  vec2 mod289(vec2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
  vec3 permute(vec3 x) { return mod289(((x*34.0)+1.0)*x); }

  float snoise(vec2 v) {
    const vec4 C = vec4(0.211324865405187, 0.366025403784439,
                        -0.577350269189626, 0.024390243902439);
    vec2 i  = floor(v + dot(v, C.yy));
    vec2 x0 = v - i + dot(i, C.xx);
    vec2 i1 = (x0.x > x0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
    vec4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;
    i = mod289(i);
    vec3 p = permute(permute(i.y + vec3(0.0, i1.y, 1.0)) + i.x + vec3(0.0, i1.x, 1.0));
    vec3 m = max(0.5 - vec3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
    m = m*m; m = m*m;
    vec3 x = 2.0 * fract(p * C.www) - 1.0;
    vec3 h = abs(x) - 0.5;
    vec3 ox = floor(x + 0.5);
    vec3 a0 = x - ox;
    m *= 1.79284291400159 - 0.85373472095314 * (a0*a0 + h*h);
    vec3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    return 130.0 * dot(m, g);
  }

  void main() {
    vec2 uv = v_texCoord;
    float t = u_time * 0.05;

    vec2 center = uv - 0.5;
    float centerWeight = 1.0 - smoothstep(0.0, 0.7, length(center));

    // Large-scale movement (slow, big blobs)
    float n1 = snoise(uv * 0.35 + vec2(t, t * 0.7));
    float n2 = snoise(uv * 0.35 + vec2(-t * 0.8, t * 0.5) + vec2(50.0, 50.0));

    // Medium-scale detail (adds organic movement)
    float n3 = snoise(uv * 0.9 + vec2(t * 1.2, -t) + vec2(100.0, 0.0));
    float n4 = snoise(uv * 0.9 + vec2(-t, t * 1.1) + vec2(0.0, 100.0));

    // Combine two octaves
    vec2 warp = vec2(
      n1 * 0.65 + n3 * 0.35,
      n2 * 0.65 + n4 * 0.35
    ) * centerWeight;

    vec2 warpedUV = uv + warp * u_intensity;
    warpedUV = clamp(warpedUV, 0.0, 1.0);

    gl_FragColor = texture2D(u_texture, warpedUV);
  }
`;
  var OUTPUT_SHADER = `
  precision highp float;
  uniform sampler2D u_texture;
  uniform float u_saturation;
  uniform float u_dithering;
  uniform float u_time;
  uniform float u_scale;
  uniform vec2 u_resolution;
  varying vec2 v_texCoord;

  highp float hash(highp vec3 p) {
    p = fract(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return fract((p.x + p.y) * p.z);
  }

  void main() {
    vec2 uv = (v_texCoord - 0.5) / u_scale + 0.5;
    uv = clamp(uv, 0.0, 1.0);

    vec4 color = texture2D(u_texture, uv);

    vec2 center = v_texCoord - 0.5;
    float vignette = 1.0 - dot(center, center) * 0.3;
    color.rgb *= vignette;

    float gray = dot(color.rgb, vec3(0.299, 0.587, 0.114));
    color.rgb = mix(vec3(gray), color.rgb, u_saturation);

    highp vec2 pixelPos = floor(v_texCoord * u_resolution);
    highp float noise = hash(vec3(pixelPos, floor(u_time * 60.0)));
    color.rgb += (noise - 0.5) * u_dithering;

    gl_FragColor = color;
  }
`;
  var Kawarp = class {
    canvas;
    gl;
    halfFloatExt = null;
    halfFloatLinearExt = null;
    // Shader programs
    blurProgram;
    blendProgram;
    tintProgram;
    warpProgram;
    outputProgram;
    // Buffers
    positionBuffer;
    texCoordBuffer;
    // Source texture (original image)
    sourceTexture;
    // Small FBOs for blur (BLUR_SIZE x BLUR_SIZE)
    blurFBO1;
    blurFBO2;
    // Album FBOs for crossfade (BLUR_SIZE x BLUR_SIZE)
    currentAlbumFBO;
    nextAlbumFBO;
    // Full-res FBO for warp output
    warpFBO;
    // Animation state
    animationId = null;
    lastFrameTime = 0;
    accumulatedTime = 0;
    isPlaying = false;
    // Transition state
    isTransitioning = false;
    transitionStartTime = 0;
    _transitionDuration;
    // Options
    _warpIntensity;
    _blurPasses;
    _animationSpeed;
    _targetAnimationSpeed;
    _saturation;
    _tintColor;
    _tintIntensity;
    _dithering;
    _scale;
    hasImage = false;
    // Cached attribute locations
    attribs;
    // Cached uniform locations
    uniforms;
    constructor(canvas, options = {}) {
      this.canvas = canvas;
      const gl = canvas.getContext("webgl", { preserveDrawingBuffer: true });
      if (!gl)
        throw new Error("WebGL not supported");
      this.gl = gl;
      this.halfFloatExt = gl.getExtension("OES_texture_half_float");
      this.halfFloatLinearExt = gl.getExtension("OES_texture_half_float_linear");
      this._warpIntensity = options.warpIntensity ?? 1;
      this._blurPasses = options.blurPasses ?? 8;
      this._animationSpeed = options.animationSpeed ?? 1;
      this._targetAnimationSpeed = this._animationSpeed;
      this._transitionDuration = options.transitionDuration ?? 1e3;
      this._saturation = options.saturation ?? 1.5;
      this._tintColor = options.tintColor ?? [0.157, 0.157, 0.235];
      this._tintIntensity = options.tintIntensity ?? 0.15;
      this._dithering = options.dithering ?? 8e-3;
      this._scale = options.scale ?? 1;
      this.blurProgram = this.createProgram(VERTEX_SHADER, KAWASE_BLUR_SHADER);
      this.blendProgram = this.createProgram(VERTEX_SHADER, BLEND_SHADER);
      this.tintProgram = this.createProgram(VERTEX_SHADER, TINT_SHADER);
      this.warpProgram = this.createProgram(VERTEX_SHADER, DOMAIN_WARP_SHADER);
      this.outputProgram = this.createProgram(VERTEX_SHADER, OUTPUT_SHADER);
      this.attribs = {
        position: gl.getAttribLocation(this.blurProgram, "a_position"),
        texCoord: gl.getAttribLocation(this.blurProgram, "a_texCoord")
      };
      this.uniforms = {
        blur: {
          resolution: gl.getUniformLocation(this.blurProgram, "u_resolution"),
          texture: gl.getUniformLocation(this.blurProgram, "u_texture"),
          offset: gl.getUniformLocation(this.blurProgram, "u_offset")
        },
        blend: {
          texture1: gl.getUniformLocation(this.blendProgram, "u_texture1"),
          texture2: gl.getUniformLocation(this.blendProgram, "u_texture2"),
          blend: gl.getUniformLocation(this.blendProgram, "u_blend")
        },
        warp: {
          texture: gl.getUniformLocation(this.warpProgram, "u_texture"),
          time: gl.getUniformLocation(this.warpProgram, "u_time"),
          intensity: gl.getUniformLocation(this.warpProgram, "u_intensity")
        },
        tint: {
          texture: gl.getUniformLocation(this.tintProgram, "u_texture"),
          tintColor: gl.getUniformLocation(this.tintProgram, "u_tintColor"),
          tintIntensity: gl.getUniformLocation(this.tintProgram, "u_tintIntensity")
        },
        output: {
          texture: gl.getUniformLocation(this.outputProgram, "u_texture"),
          saturation: gl.getUniformLocation(this.outputProgram, "u_saturation"),
          dithering: gl.getUniformLocation(this.outputProgram, "u_dithering"),
          time: gl.getUniformLocation(this.outputProgram, "u_time"),
          scale: gl.getUniformLocation(this.outputProgram, "u_scale"),
          resolution: gl.getUniformLocation(this.outputProgram, "u_resolution")
        }
      };
      this.positionBuffer = this.createBuffer(new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]));
      this.texCoordBuffer = this.createBuffer(new Float32Array([0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1]));
      this.sourceTexture = this.createTexture();
      this.blurFBO1 = this.createFramebuffer(BLUR_SIZE, BLUR_SIZE, true);
      this.blurFBO2 = this.createFramebuffer(BLUR_SIZE, BLUR_SIZE, true);
      this.currentAlbumFBO = this.createFramebuffer(BLUR_SIZE, BLUR_SIZE, true);
      this.nextAlbumFBO = this.createFramebuffer(BLUR_SIZE, BLUR_SIZE, true);
      this.warpFBO = this.createFramebuffer(1, 1, true);
      this.resize();
    }
    // Getters and setters
    get warpIntensity() {
      return this._warpIntensity;
    }
    set warpIntensity(value) {
      this._warpIntensity = Math.max(0, Math.min(1, value));
    }
    get blurPasses() {
      return this._blurPasses;
    }
    set blurPasses(value) {
      const newValue = Math.max(1, Math.min(40, Math.floor(value)));
      if (newValue !== this._blurPasses) {
        this._blurPasses = newValue;
        if (this.hasImage) {
          this.reblurCurrentImage();
        }
      }
    }
    get animationSpeed() {
      return this._targetAnimationSpeed;
    }
    set animationSpeed(value) {
      this._targetAnimationSpeed = Math.max(0.1, Math.min(5, value));
    }
    get transitionDuration() {
      return this._transitionDuration;
    }
    set transitionDuration(value) {
      this._transitionDuration = Math.max(0, Math.min(5e3, value));
    }
    get saturation() {
      return this._saturation;
    }
    set saturation(value) {
      this._saturation = Math.max(0, Math.min(3, value));
    }
    get tintColor() {
      return this._tintColor;
    }
    set tintColor(value) {
      const newValue = value.map((v2) => Math.max(0, Math.min(1, v2)));
      const changed = newValue.some((v2, i2) => v2 !== this._tintColor[i2]);
      if (changed) {
        this._tintColor = newValue;
        if (this.hasImage) {
          this.reblurCurrentImage();
        }
      }
    }
    get tintIntensity() {
      return this._tintIntensity;
    }
    set tintIntensity(value) {
      const newValue = Math.max(0, Math.min(1, value));
      if (newValue !== this._tintIntensity) {
        this._tintIntensity = newValue;
        if (this.hasImage) {
          this.reblurCurrentImage();
        }
      }
    }
    get dithering() {
      return this._dithering;
    }
    set dithering(value) {
      this._dithering = Math.max(0, Math.min(0.1, value));
    }
    get scale() {
      return this._scale;
    }
    set scale(value) {
      this._scale = Math.max(0.01, Math.min(4, value));
    }
    setOptions(options) {
      if (options.warpIntensity !== void 0)
        this.warpIntensity = options.warpIntensity;
      if (options.blurPasses !== void 0)
        this.blurPasses = options.blurPasses;
      if (options.animationSpeed !== void 0)
        this.animationSpeed = options.animationSpeed;
      if (options.transitionDuration !== void 0)
        this.transitionDuration = options.transitionDuration;
      if (options.saturation !== void 0)
        this.saturation = options.saturation;
      if (options.tintColor !== void 0)
        this.tintColor = options.tintColor;
      if (options.tintIntensity !== void 0)
        this.tintIntensity = options.tintIntensity;
      if (options.dithering !== void 0)
        this.dithering = options.dithering;
      if (options.scale !== void 0)
        this.scale = options.scale;
    }
    getOptions() {
      return {
        warpIntensity: this._warpIntensity,
        blurPasses: this._blurPasses,
        animationSpeed: this._targetAnimationSpeed,
        transitionDuration: this._transitionDuration,
        saturation: this._saturation,
        tintColor: this._tintColor,
        tintIntensity: this._tintIntensity,
        dithering: this._dithering,
        scale: this._scale
      };
    }
    // Image loading methods
    loadImage(src) {
      return new Promise((resolve, reject) => {
        const img = new Image();
        img.crossOrigin = "anonymous";
        img.onload = () => {
          this.gl.bindTexture(this.gl.TEXTURE_2D, this.sourceTexture);
          this.gl.texImage2D(this.gl.TEXTURE_2D, 0, this.gl.RGBA, this.gl.RGBA, this.gl.UNSIGNED_BYTE, img);
          this.processNewImage();
          resolve();
        };
        img.onerror = () => reject(new Error(`Failed to load image: ${src}`));
        img.src = src;
      });
    }
    loadImageElement(source) {
      this.gl.bindTexture(this.gl.TEXTURE_2D, this.sourceTexture);
      this.gl.texImage2D(this.gl.TEXTURE_2D, 0, this.gl.RGBA, this.gl.RGBA, this.gl.UNSIGNED_BYTE, source);
      this.processNewImage();
    }
    loadImageData(data, width, height) {
      this.gl.bindTexture(this.gl.TEXTURE_2D, this.sourceTexture);
      this.gl.texImage2D(this.gl.TEXTURE_2D, 0, this.gl.RGBA, width, height, 0, this.gl.RGBA, this.gl.UNSIGNED_BYTE, data instanceof Uint8ClampedArray ? new Uint8Array(data.buffer) : data);
      this.processNewImage();
    }
    loadFromImageData(imageData) {
      this.loadImageData(imageData.data, imageData.width, imageData.height);
    }
    async loadBlob(blob) {
      const bitmap = await createImageBitmap(blob);
      this.loadImageElement(bitmap);
      bitmap.close();
    }
    loadBase64(base64) {
      const src = base64.startsWith("data:") ? base64 : `data:image/png;base64,${base64}`;
      return this.loadImage(src);
    }
    async loadArrayBuffer(buffer, mimeType = "image/png") {
      const blob = new Blob([buffer], { type: mimeType });
      return this.loadBlob(blob);
    }
    loadGradient(colors, angle = 135) {
      const size = 512;
      const canvas = document.createElement("canvas");
      canvas.width = size;
      canvas.height = size;
      const ctx = canvas.getContext("2d");
      if (!ctx)
        return;
      const angleRad = angle * Math.PI / 180;
      const x1 = size / 2 - Math.cos(angleRad) * size;
      const y1 = size / 2 - Math.sin(angleRad) * size;
      const x2 = size / 2 + Math.cos(angleRad) * size;
      const y2 = size / 2 + Math.sin(angleRad) * size;
      const gradient = ctx.createLinearGradient(x1, y1, x2, y2);
      colors.forEach((color, i2) => {
        gradient.addColorStop(i2 / (colors.length - 1), color);
      });
      ctx.fillStyle = gradient;
      ctx.fillRect(0, 0, size, size);
      this.loadImageElement(canvas);
    }
    /**
     * Process a new image: blur it and start transition
     * This is the key optimization - blur only runs here, not every frame!
     */
    processNewImage() {
      [this.currentAlbumFBO, this.nextAlbumFBO] = [
        this.nextAlbumFBO,
        this.currentAlbumFBO
      ];
      this.blurSourceInto(this.nextAlbumFBO);
      this.hasImage = true;
      this.isTransitioning = true;
      this.transitionStartTime = performance.now();
    }
    /**
     * Re-blur the current image (used when blurPasses changes)
     * Updates nextAlbumFBO in place without starting a transition
     */
    reblurCurrentImage() {
      this.blurSourceInto(this.nextAlbumFBO);
    }
    /**
     * Blur the source texture into the target FBO (with tint applied before blur)
     */
    blurSourceInto(targetFBO) {
      const gl = this.gl;
      gl.useProgram(this.tintProgram);
      this.setupAttributes();
      gl.bindFramebuffer(gl.FRAMEBUFFER, this.blurFBO1.framebuffer);
      gl.viewport(0, 0, BLUR_SIZE, BLUR_SIZE);
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D, this.sourceTexture);
      gl.uniform1i(this.uniforms.tint.texture, 0);
      gl.uniform3fv(this.uniforms.tint.tintColor, this._tintColor);
      gl.uniform1f(this.uniforms.tint.tintIntensity, this._tintIntensity);
      gl.drawArrays(gl.TRIANGLES, 0, 6);
      gl.useProgram(this.blurProgram);
      this.setupAttributes();
      gl.uniform2f(this.uniforms.blur.resolution, BLUR_SIZE, BLUR_SIZE);
      gl.uniform1i(this.uniforms.blur.texture, 0);
      let readFBO = this.blurFBO1;
      let writeFBO = this.blurFBO2;
      for (let i2 = 0; i2 < this._blurPasses; i2++) {
        gl.bindFramebuffer(gl.FRAMEBUFFER, writeFBO.framebuffer);
        gl.viewport(0, 0, BLUR_SIZE, BLUR_SIZE);
        gl.bindTexture(gl.TEXTURE_2D, readFBO.texture);
        gl.uniform1f(this.uniforms.blur.offset, i2 + 0.5);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
        [readFBO, writeFBO] = [writeFBO, readFBO];
      }
      gl.bindFramebuffer(gl.FRAMEBUFFER, targetFBO.framebuffer);
      gl.viewport(0, 0, BLUR_SIZE, BLUR_SIZE);
      gl.bindTexture(gl.TEXTURE_2D, readFBO.texture);
      gl.uniform1f(this.uniforms.blur.offset, 0);
      gl.drawArrays(gl.TRIANGLES, 0, 6);
    }
    resize() {
      const width = this.canvas.width;
      const height = this.canvas.height;
      if (this.warpFBO)
        this.deleteFramebuffer(this.warpFBO);
      this.warpFBO = this.createFramebuffer(width, height, true);
    }
    start() {
      if (this.isPlaying)
        return;
      this.isPlaying = true;
      this.lastFrameTime = performance.now();
      requestAnimationFrame(this.renderLoop);
    }
    stop() {
      this.isPlaying = false;
      if (this.animationId !== null) {
        cancelAnimationFrame(this.animationId);
        this.animationId = null;
      }
    }
    renderFrame(time) {
      const now = performance.now();
      if (time !== void 0) {
        this.render(time, now);
      } else {
        const dt = (now - this.lastFrameTime) / 1e3;
        this.lastFrameTime = now;
        this._animationSpeed += (this._targetAnimationSpeed - this._animationSpeed) * 0.05;
        this.accumulatedTime += dt * this._animationSpeed;
        this.render(this.accumulatedTime, now);
      }
    }
    dispose() {
      this.stop();
      const gl = this.gl;
      gl.deleteProgram(this.blurProgram);
      gl.deleteProgram(this.blendProgram);
      gl.deleteProgram(this.tintProgram);
      gl.deleteProgram(this.warpProgram);
      gl.deleteProgram(this.outputProgram);
      gl.deleteBuffer(this.positionBuffer);
      gl.deleteBuffer(this.texCoordBuffer);
      gl.deleteTexture(this.sourceTexture);
      this.deleteFramebuffer(this.blurFBO1);
      this.deleteFramebuffer(this.blurFBO2);
      this.deleteFramebuffer(this.currentAlbumFBO);
      this.deleteFramebuffer(this.nextAlbumFBO);
      this.deleteFramebuffer(this.warpFBO);
    }
    renderLoop = (timestamp) => {
      if (!this.isPlaying)
        return;
      const dt = (timestamp - this.lastFrameTime) / 1e3;
      this.lastFrameTime = timestamp;
      this._animationSpeed += (this._targetAnimationSpeed - this._animationSpeed) * 0.05;
      this.accumulatedTime += dt * this._animationSpeed;
      this.render(this.accumulatedTime, timestamp);
      this.animationId = requestAnimationFrame(this.renderLoop);
    };
    /**
     * Main render loop - very efficient!
     * Just: blend album FBOs → domain warp → output
     */
    render(time, timestamp = performance.now()) {
      const gl = this.gl;
      const width = this.canvas.width;
      const height = this.canvas.height;
      let blendFactor = 1;
      if (this.isTransitioning) {
        const elapsed = timestamp - this.transitionStartTime;
        blendFactor = Math.min(1, elapsed / this._transitionDuration);
        if (blendFactor >= 1) {
          this.isTransitioning = false;
        }
      }
      let blendedTexture;
      if (this.isTransitioning && blendFactor < 1) {
        gl.useProgram(this.blendProgram);
        this.setupAttributes();
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.blurFBO1.framebuffer);
        gl.viewport(0, 0, BLUR_SIZE, BLUR_SIZE);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, this.currentAlbumFBO.texture);
        gl.uniform1i(this.uniforms.blend.texture1, 0);
        gl.activeTexture(gl.TEXTURE1);
        gl.bindTexture(gl.TEXTURE_2D, this.nextAlbumFBO.texture);
        gl.uniform1i(this.uniforms.blend.texture2, 1);
        gl.uniform1f(this.uniforms.blend.blend, blendFactor);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
        blendedTexture = this.blurFBO1.texture;
        gl.useProgram(this.warpProgram);
        this.setupAttributes();
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.warpFBO.framebuffer);
        gl.viewport(0, 0, width, height);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, blendedTexture);
        gl.uniform1i(this.uniforms.warp.texture, 0);
        gl.uniform1f(this.uniforms.warp.time, time);
        gl.uniform1f(this.uniforms.warp.intensity, this._warpIntensity);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
        gl.useProgram(this.outputProgram);
        this.setupAttributes();
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.viewport(0, 0, width, height);
        gl.bindTexture(gl.TEXTURE_2D, this.warpFBO.texture);
        gl.uniform1i(this.uniforms.output.texture, 0);
        gl.uniform1f(this.uniforms.output.saturation, this._saturation);
        gl.uniform1f(this.uniforms.output.dithering, this._dithering);
        gl.uniform1f(this.uniforms.output.time, time);
        gl.uniform1f(this.uniforms.output.scale, this._scale);
        gl.uniform2f(this.uniforms.output.resolution, width, height);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
      } else {
        gl.useProgram(this.warpProgram);
        this.setupAttributes();
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.warpFBO.framebuffer);
        gl.viewport(0, 0, width, height);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, this.nextAlbumFBO.texture);
        gl.uniform1i(this.uniforms.warp.texture, 0);
        gl.uniform1f(this.uniforms.warp.time, time);
        gl.uniform1f(this.uniforms.warp.intensity, this._warpIntensity);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
        gl.useProgram(this.outputProgram);
        this.setupAttributes();
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.viewport(0, 0, width, height);
        gl.bindTexture(gl.TEXTURE_2D, this.warpFBO.texture);
        gl.uniform1i(this.uniforms.output.texture, 0);
        gl.uniform1f(this.uniforms.output.saturation, this._saturation);
        gl.uniform1f(this.uniforms.output.dithering, this._dithering);
        gl.uniform1f(this.uniforms.output.time, time);
        gl.uniform1f(this.uniforms.output.scale, this._scale);
        gl.uniform2f(this.uniforms.output.resolution, width, height);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
      }
    }
    setupAttributes() {
      const gl = this.gl;
      gl.bindBuffer(gl.ARRAY_BUFFER, this.positionBuffer);
      gl.enableVertexAttribArray(this.attribs.position);
      gl.vertexAttribPointer(this.attribs.position, 2, gl.FLOAT, false, 0, 0);
      gl.bindBuffer(gl.ARRAY_BUFFER, this.texCoordBuffer);
      gl.enableVertexAttribArray(this.attribs.texCoord);
      gl.vertexAttribPointer(this.attribs.texCoord, 2, gl.FLOAT, false, 0, 0);
    }
    createShader(type, source) {
      const gl = this.gl;
      const shader = gl.createShader(type);
      if (!shader)
        throw new Error("Failed to create shader");
      gl.shaderSource(shader, source);
      gl.compileShader(shader);
      if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const error = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`Shader compile error: ${error}`);
      }
      return shader;
    }
    createProgram(vertexSource, fragmentSource) {
      const gl = this.gl;
      const vertexShader = this.createShader(gl.VERTEX_SHADER, vertexSource);
      const fragmentShader = this.createShader(gl.FRAGMENT_SHADER, fragmentSource);
      const program = gl.createProgram();
      if (!program)
        throw new Error("Failed to create program");
      gl.attachShader(program, vertexShader);
      gl.attachShader(program, fragmentShader);
      gl.linkProgram(program);
      if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const error = gl.getProgramInfoLog(program);
        gl.deleteProgram(program);
        throw new Error(`Program link error: ${error}`);
      }
      gl.deleteShader(vertexShader);
      gl.deleteShader(fragmentShader);
      return program;
    }
    createBuffer(data) {
      const gl = this.gl;
      const buffer = gl.createBuffer();
      if (!buffer)
        throw new Error("Failed to create buffer");
      gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferData(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);
      return buffer;
    }
    createTexture() {
      const gl = this.gl;
      const texture = gl.createTexture();
      if (!texture)
        throw new Error("Failed to create texture");
      gl.bindTexture(gl.TEXTURE_2D, texture);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      return texture;
    }
    createFramebuffer(width, height, useHighPrecision = false) {
      const gl = this.gl;
      const texture = this.createTexture();
      const canUseHalfFloat = useHighPrecision && this.halfFloatExt && this.halfFloatLinearExt;
      const type = canUseHalfFloat ? this.halfFloatExt.HALF_FLOAT_OES : gl.UNSIGNED_BYTE;
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, type, null);
      const framebuffer = gl.createFramebuffer();
      if (!framebuffer)
        throw new Error("Failed to create framebuffer");
      gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);
      gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0);
      return { framebuffer, texture };
    }
    deleteFramebuffer(fbo) {
      this.gl.deleteFramebuffer(fbo.framebuffer);
      this.gl.deleteTexture(fbo.texture);
    }
  };

  // src/settings/shared.ts
  function clamp(n, min, max) {
    return Math.min(max, Math.max(min, n));
  }
  function sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
  function readLS(key, fallback) {
    const value = localStorage.getItem(key);
    return value === null || value === "" ? fallback : value;
  }
  function readNum(key, fallback) {
    const raw = localStorage.getItem(key);
    const parsed = raw === null ? NaN : parseInt(raw, 10);
    return Number.isFinite(parsed) ? parsed : fallback;
  }
  function ensureStyleTag(id) {
    let style = document.getElementById(id);
    if (!style) {
      style = document.createElement("style");
      style.id = id;
      document.head.appendChild(style);
    }
    return style;
  }
  function updateStyle(id, css) {
    ensureStyleTag(id).textContent = css;
  }
  function hasMountedCanvasMedia(el) {
    if (!el) return false;
    if (el.querySelector("video")) return true;
    const img = el.querySelector("img");
    return !!img && !!img.getAttribute("src");
  }
  function getOsName() {
    return (Spicetify?.Platform?.PlatformData?.os_name || navigator.platform || "").toString().toLowerCase();
  }
  function isUnixLikeOS() {
    const os = getOsName();
    return os.includes("linux") || os.includes("mac") || os.includes("darwin") || os.includes("osx") || os.includes("macos");
  }

  // src/settings/features/backgroundAppearance.ts
  var BG_ENGINE_KEY = "liquify-bg-engine";
  function getBgEngine() {
    return localStorage.getItem(BG_ENGINE_KEY) === "tiles" ? "tiles" : "kawarp";
  }
  function setBgEngine(engine) {
    localStorage.setItem(BG_ENGINE_KEY, engine);
    applyBackgroundAppearance();
    window.dispatchEvent(new Event("liquifyBackgroundChange"));
  }
  var HIRES_COVER_KEY = "liquify-hires-cover";
  function isHiResCoverOn() {
    return (localStorage.getItem(HIRES_COVER_KEY) || "on") === "on";
  }
  function setHiResCover(on) {
    localStorage.setItem(HIRES_COVER_KEY, on ? "on" : "off");
    window.dispatchEvent(new Event("liquifyBackgroundChange"));
  }
  var BG_SURFACES = {
    // The two crossfading cover layers — every non-animated mode.
    static: {
      blurKey: "liquify-bg-blur",
      brightnessKey: "liquify-bg-brightness",
      blurVar: "--liquify-bg-blur",
      brightnessVar: "--liquify-bg-brightness",
      defaults: { blur: 7, brightness: 45 }
    },
    // Kawarp: the blur is rescaled into render passes in kawarpBackground.ts, so
    // only the brightness reaches CSS.
    kawarp: {
      blurKey: "liquify-kawarp-blur",
      brightnessKey: "liquify-kawarp-brightness",
      blurVar: "",
      brightnessVar: "--liquify-kawarp-brightness",
      defaults: { blur: 10, brightness: 45 }
    },
    // The drifting blob field. Its old look was hard-coded blur(50px)
    // brightness(60%); those are its defaults so nothing changes for anyone who
    // never touches the sliders.
    tiles: {
      blurKey: "liquify-tiles-blur",
      brightnessKey: "liquify-tiles-brightness",
      blurVar: "--liquify-tiles-blur",
      brightnessVar: "--liquify-tiles-brightness",
      defaults: { blur: 50, brightness: 60 }
    }
  };
  var BG_BLUR_RANGE = { static: 100, kawarp: 100, tiles: 150 };
  function readBackgroundAppearance(surface, field) {
    const spec = BG_SURFACES[surface];
    const key = field === "blur" ? spec.blurKey : spec.brightnessKey;
    return readNum(key, spec.defaults[field]);
  }
  function readAllBackgroundAppearance() {
    const all = {};
    for (const surface of Object.keys(BG_SURFACES)) {
      all[surface] = {
        blur: readBackgroundAppearance(surface, "blur"),
        brightness: readBackgroundAppearance(surface, "brightness")
      };
    }
    return all;
  }
  function applyBackgroundAppearance() {
    const style = document.documentElement.style;
    const write = (name, value) => {
      if (!name) return;
      if (style.getPropertyValue(name) === value) return;
      style.setProperty(name, value);
    };
    for (const surface of Object.keys(BG_SURFACES)) {
      const spec = BG_SURFACES[surface];
      write(spec.blurVar, `${readBackgroundAppearance(surface, "blur")}px`);
      write(spec.brightnessVar, `${readBackgroundAppearance(surface, "brightness")}%`);
    }
  }
  function setBackgroundAppearance(surface, field, value) {
    const spec = BG_SURFACES[surface];
    localStorage.setItem(field === "blur" ? spec.blurKey : spec.brightnessKey, String(value));
    applyBackgroundAppearance();
    window.dispatchEvent(new Event("liquifyBackgroundChange"));
  }
  function resetBackgroundAppearance() {
    for (const surface of Object.keys(BG_SURFACES)) {
      const spec = BG_SURFACES[surface];
      localStorage.setItem(spec.blurKey, String(spec.defaults.blur));
      localStorage.setItem(spec.brightnessKey, String(spec.defaults.brightness));
    }
    localStorage.setItem(BG_ENGINE_KEY, "kawarp");
    applyBackgroundAppearance();
  }
  function ensureBackgroundAppearanceApplied() {
    applyBackgroundAppearance();
  }

  // src/settings/features/kawarpBackground.ts
  var KAWARP_KEYS = {
    warp: "liquify-kawarp-warp",
    speed: "liquify-kawarp-speed",
    saturation: "liquify-kawarp-saturation",
    scale: "liquify-kawarp-scale",
    contrast: "liquify-kawarp-contrast"
  };
  var KAWARP_DEFAULTS = {
    warp: 50,
    speed: 100,
    saturation: 150,
    scale: 100,
    contrast: 100
  };
  var KAWARP_RANGES = {
    warp: { min: 0, max: 100 },
    speed: { min: 0, max: 400 },
    saturation: { min: 0, max: 500 },
    scale: { min: 10, max: 400 },
    contrast: { min: 0, max: 300 }
  };
  var CROSSFADE_MS = 600;
  var TARGET_FPS = 60;
  var MAX_KAWARP_PX = 1280;
  var MAX_DPR = 1.5;
  var RESIZE_SETTLE_MS = 160;
  var MAX_FRAME_STEP_MS = 250;
  function read(key) {
    const range = KAWARP_RANGES[key];
    return clamp(readNum(KAWARP_KEYS[key], KAWARP_DEFAULTS[key]), range.min, range.max);
  }
  function kawarpOptions() {
    return {
      // The generic 0-100 slider maps onto Kawarp's 0-1 intensity.
      warpIntensity: read("warp") / 100,
      // Kawarp counts blur passes (1-40) rather than pixels, so the slider is
      // rescaled. It has its own stored value — see backgroundAppearance.ts.
      blurPasses: Math.max(
        1,
        Math.round(clamp(readBackgroundAppearance("kawarp", "blur"), 0, 100) / 100 * 40)
      ),
      // Only informational here: the shared loop below advances its own clock, so
      // the speed is applied there rather than by Kawarp's accumulator.
      animationSpeed: read("speed") / 100,
      // Kawarp's own blend stays off — the crossfade is the two-canvas stack in
      // swap(), so each renderer only ever holds one still image.
      transitionDuration: 0,
      saturation: read("saturation") / 100,
      scale: clamp(read("scale") / 100, 0.01, 4)
    };
  }
  function applyKawarpAppearance() {
    const style = document.documentElement.style;
    const value = `${read("contrast")}%`;
    if (style.getPropertyValue("--liquify-kawarp-contrast") !== value) {
      style.setProperty("--liquify-kawarp-contrast", value);
    }
  }
  function setKawarpValue(key, value) {
    localStorage.setItem(KAWARP_KEYS[key], String(value));
    applyKawarpAppearance();
    window.dispatchEvent(new Event("liquifyBackgroundChange"));
  }
  function resetKawarpDefaults() {
    for (const key of Object.keys(KAWARP_KEYS)) {
      localStorage.setItem(KAWARP_KEYS[key], String(KAWARP_DEFAULTS[key]));
    }
    applyKawarpAppearance();
  }
  var KawarpBackdrop = class {
    constructor() {
      this.layers = [];
      this.useA = true;
      this.swapTimer = 0;
      this.resizeTimer = 0;
      this.resizeObserver = null;
      // Guards against a slow decode resolving after the mode moved on.
      this.token = 0;
      this.lastUrl = "";
      this.lastOptions = "";
      this.active = false;
      // WebGL refused a context, or the picture failed to decode with CORS. Either
      // way the caller has to paint something else.
      this.failed = false;
      this.failedUrls = /* @__PURE__ */ new Set();
      // --- Shared render loop ---
      this.live = /* @__PURE__ */ new Set();
      this.rafId = 0;
      this.lastFrame = 0;
      /** The warp's own time. Shared, so both renderers stay in phase. */
      this.clock = 0;
      this.speed = 1;
      this.tick = (now) => {
        this.rafId = requestAnimationFrame(this.tick);
        const elapsed = now - this.lastFrame;
        if (elapsed < 1e3 / TARGET_FPS - 4) return;
        this.lastFrame = now;
        this.clock += Math.min(elapsed, MAX_FRAME_STEP_MS) / 1e3 * this.speed;
        for (const renderer of this.live) {
          try {
            renderer.renderFrame(this.clock);
          } catch {
          }
        }
      };
      this.el = document.createElement("div");
      this.el.className = "liquify-kawarp-bg";
      this.el.setAttribute("aria-hidden", "true");
      document.addEventListener("visibilitychange", () => this.syncLoop());
    }
    /** Whether this URL can be shown. A picture Kawarp could not read (an image
     *  host that sends no Access-Control-Allow-Origin — WebGL needs the pixels,
     *  unlike a plain CSS background) has to fall back to the static layers. */
    canRender(url) {
      return !this.failed && !!url && !this.failedUrls.has(url);
    }
    setActive(active) {
      if (this.active === active) return;
      this.active = active;
      this.el.classList.toggle("active", active);
      this.syncLoop();
    }
    /** Re-reads the settings and pushes them into both renderers. */
    applyOptions() {
      const options = kawarpOptions();
      this.speed = options.animationSpeed;
      if (this.layers.length === 0) return;
      const next = JSON.stringify(options);
      if (next === this.lastOptions) return;
      this.lastOptions = next;
      for (const layer of this.layers) layer.renderer.setOptions(options);
    }
    /** Paints `url`, crossfading from whatever is already on screen. */
    show(url) {
      if (!this.ensureLayers()) return;
      this.applyOptions();
      if (url === this.lastUrl) return;
      this.lastUrl = url;
      void this.swap(url);
    }
    // --- The loop -------------------------------------------------------------
    syncLoop() {
      const wanted = this.active && this.live.size > 0 && !document.hidden;
      if (wanted && !this.rafId) {
        this.lastFrame = performance.now();
        this.rafId = requestAnimationFrame(this.tick);
      } else if (!wanted && this.rafId) {
        cancelAnimationFrame(this.rafId);
        this.rafId = 0;
      }
    }
    // --- Layers ---------------------------------------------------------------
    ensureLayers() {
      if (this.failed) return false;
      if (this.layers.length === 2) return true;
      try {
        this.layers = [0, 1].map(() => {
          const canvas = document.createElement("canvas");
          canvas.className = "liquify-kawarp-canvas";
          this.el.appendChild(canvas);
          return { canvas, renderer: new Kawarp(canvas, kawarpOptions()) };
        });
      } catch (error) {
        console.warn("[Liquify] Kawarp unavailable, falling back to the static background.", error);
        this.failed = true;
        this.el.replaceChildren();
        this.layers = [];
        return false;
      }
      for (const layer of this.layers) this.sizeCanvas(layer);
      this.observeResize();
      this.lastOptions = JSON.stringify(kawarpOptions());
      return true;
    }
    async swap(url) {
      const token = ++this.token;
      let source;
      try {
        source = await decodeImage(url);
      } catch {
        this.failedUrls.add(url);
        if (token === this.token) this.lastUrl = "";
        window.dispatchEvent(new Event("liquifyBackgroundChange"));
        return;
      }
      if (token !== this.token || this.layers.length < 2) return;
      const incoming = this.useA ? this.layers[0] : this.layers[1];
      const outgoing = this.useA ? this.layers[1] : this.layers[0];
      this.sizeCanvas(incoming);
      incoming.renderer.loadImageElement(source);
      this.live.add(incoming.renderer);
      this.syncLoop();
      try {
        incoming.renderer.renderFrame(this.clock);
      } catch {
      }
      await nextFrame();
      if (token !== this.token) return;
      incoming.canvas.classList.add("active", "is-front");
      outgoing.canvas.classList.remove("is-front");
      this.useA = !this.useA;
      window.clearTimeout(this.swapTimer);
      this.swapTimer = window.setTimeout(() => {
        if (token !== this.token) return;
        outgoing.canvas.classList.remove("active");
        this.live.delete(outgoing.renderer);
        this.syncLoop();
      }, CROSSFADE_MS + 80);
    }
    observeResize() {
      if (this.resizeObserver) return;
      this.resizeObserver = new ResizeObserver(() => {
        window.clearTimeout(this.resizeTimer);
        this.resizeTimer = window.setTimeout(() => {
          for (const layer of this.layers) this.sizeCanvas(layer);
        }, RESIZE_SETTLE_MS);
      });
      this.resizeObserver.observe(this.el);
    }
    /** Matches a renderer's backing store to the space it is stretched across.
     *
     *  Kawarp draws at canvas.width/height and never touches them — its resize()
     *  only reallocates the warp buffer to whatever they already say. Left unset,
     *  every frame renders at a bare canvas's 300x150 and is scaled up by CSS. */
    sizeCanvas(layer) {
      const width = this.el.clientWidth;
      const height = this.el.clientHeight;
      if (width < 2 || height < 2) return;
      const dpr = Math.min(window.devicePixelRatio || 1, MAX_DPR);
      const longest = Math.max(width, height) * dpr;
      const factor = longest > MAX_KAWARP_PX ? MAX_KAWARP_PX / longest * dpr : dpr;
      const target = { width: Math.round(width * factor), height: Math.round(height * factor) };
      if (layer.canvas.width === target.width && layer.canvas.height === target.height) return;
      layer.canvas.width = target.width;
      layer.canvas.height = target.height;
      layer.renderer.resize();
      try {
        layer.renderer.renderFrame(this.clock);
      } catch {
      }
    }
  };
  function decodeImage(url) {
    const image = new Image();
    image.crossOrigin = "anonymous";
    image.src = url;
    return image.decode().then(() => image);
  }
  function nextFrame() {
    return new Promise((resolve) => requestAnimationFrame(() => resolve()));
  }

  // src/settings/features/backgroundLibrary.ts
  var DB_NAME = "liquify-backgrounds";
  var DB_VERSION = 1;
  var STORE = "images";
  var LIBRARY_SELECTED_KEY = "liquify-bg-library-id";
  var LEGACY_IMAGE_KEY = "liquify-bg-image";
  var dbPromise = null;
  function openDb() {
    if (dbPromise) return dbPromise;
    dbPromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB_NAME, DB_VERSION);
      request.onupgradeneeded = () => {
        const db = request.result;
        if (!db.objectStoreNames.contains(STORE)) db.createObjectStore(STORE, { keyPath: "id" });
      };
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
    return dbPromise;
  }
  function tx(mode, run) {
    return openDb().then(
      (db) => new Promise((resolve, reject) => {
        const request = run(db.transaction(STORE, mode).objectStore(STORE));
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
      })
    );
  }
  async function listImages() {
    try {
      const all = await tx("readonly", (s2) => s2.getAll()) || [];
      return all.sort((a, b2) => b2.added - a.added);
    } catch {
      return [];
    }
  }
  async function addImages(files) {
    for (const file of files) {
      if (!file.type.startsWith("image/")) continue;
      const entry = {
        id: `${Date.now()}-${Math.random().toString(36).slice(2, 9)}`,
        name: file.name,
        added: Date.now(),
        blob: file
      };
      await tx("readwrite", (s2) => s2.put(entry));
    }
  }
  async function deleteImage(id) {
    await tx("readwrite", (s2) => s2.delete(id));
    if (localStorage.getItem(LIBRARY_SELECTED_KEY) === id) {
      localStorage.removeItem(LIBRARY_SELECTED_KEY);
      await refreshSelected();
    }
  }
  var currentUrl = null;
  var currentId = "";
  function getLibraryUrl() {
    return currentUrl;
  }
  function revoke() {
    if (currentUrl && currentUrl.startsWith("blob:")) URL.revokeObjectURL(currentUrl);
    currentUrl = null;
  }
  async function refreshSelected() {
    const id = localStorage.getItem(LIBRARY_SELECTED_KEY) || "";
    if (!id) {
      revoke();
      currentId = "";
      currentUrl = localStorage.getItem(LEGACY_IMAGE_KEY) || null;
      window.dispatchEvent(new Event("liquifyBackgroundChange"));
      return;
    }
    if (id === currentId && currentUrl) return;
    try {
      const entry = await tx("readonly", (s2) => s2.get(id));
      revoke();
      currentId = id;
      currentUrl = entry?.blob ? URL.createObjectURL(entry.blob) : null;
    } catch {
      revoke();
      currentId = "";
    }
    window.dispatchEvent(new Event("liquifyBackgroundChange"));
  }
  function selectImage(id) {
    localStorage.setItem(LIBRARY_SELECTED_KEY, id);
    void refreshSelected();
  }
  function ensureLibraryApplied() {
    void refreshSelected();
  }

  // src/background.ts
  var FALLBACK_COLOR = "rgb(30,215,96)";
  var EMPTY_BACKDROP = "linear-gradient(135deg, rgb(32,32,38) 0%, rgb(20,20,25) 50%, rgb(26,23,33) 100%)";
  function cssImage(source) {
    return source.startsWith("linear-gradient(") ? source : `url("${source}")`;
  }
  var UNSAMPLEABLE = "liquify:unsampleable";
  var IMAGE_SIZE_UPGRADES = {
    // Album / track art: 64 and 300 → 640.
    ab67616d00004851: "ab67616d0000b273",
    ab67616d00001e02: "ab67616d0000b273",
    // Artist images: 160 and 320 → 640.
    ab6761610000f178: "ab6761610000e5eb",
    ab67616100005174: "ab6761610000e5eb",
    // Playlist mosaics: 300 → 640.
    ab67706c0000da84: "ab67706c0000bebb"
  };
  function upgradeImageSize(url) {
    return url.replace(/\/image\/([0-9a-f]{16})/i, (whole, code) => {
      const larger = IMAGE_SIZE_UPGRADES[code.toLowerCase()];
      return larger ? `/image/${larger}` : whole;
    });
  }
  var COVER_PREFIX_2000 = "ab67616d000082c1";
  var coverSize = /* @__PURE__ */ new Map();
  var coverPending = /* @__PURE__ */ new Set();
  function resolveBigCover(base, big) {
    if (coverPending.has(base)) return;
    coverPending.add(base);
    const probe = new Image();
    const settle = (url) => {
      coverPending.delete(base);
      coverSize.set(base, url);
      if (base === baseCoverUrlOf(Spicetify.Player?.data?.item)) {
        window.dispatchEvent(new Event("liquifyBackgroundChange"));
      }
    };
    probe.onload = () => settle(big);
    probe.onerror = () => settle(base);
    probe.src = big;
  }
  function baseCoverUrlOf(item) {
    const meta = (item?.contextTrack || item)?.metadata;
    const raw = meta?.image_xlarge_url || meta?.image_large_url || meta?.image_url || meta?.image_small_url;
    if (!raw) return null;
    const url = String(raw).replace("spotify:image:", "https://i.scdn.co/image/");
    return isHiResCoverOn() ? upgradeImageSize(url) : url;
  }
  function bigCoverUrlOf(base) {
    if (!isHiResCoverOn()) return null;
    const big = base.replace(/\/image\/ab67616d[0-9a-f]{8}/i, `/image/${COVER_PREFIX_2000}`);
    return big === base ? null : big;
  }
  function getCoverUrl() {
    const base = baseCoverUrlOf(Spicetify.Player?.data?.item);
    if (!base) return null;
    const big = bigCoverUrlOf(base);
    if (!big) return base;
    const settled = coverSize.get(base);
    if (settled) return settled;
    resolveBigCover(base, big);
    return null;
  }
  function prefetchNeighbourCovers() {
    const queue = Spicetify.Queue || Spicetify.Platform?.PlayerAPI?._queue || {};
    const around = [
      ...(queue.nextTracks || queue.next_tracks || []).slice(0, 2),
      ...(queue.prevTracks || queue.prev_tracks || []).slice(-1)
    ];
    for (const track of around) {
      const base = baseCoverUrlOf(track);
      if (!base || coverSize.has(base)) continue;
      const big = bigCoverUrlOf(base);
      if (big) resolveBigCover(base, big);
    }
  }
  var MAX_SAMPLE_PIXELS = 128 * 128;
  function averageColorOf(img) {
    try {
      const scale = Math.min(1, Math.sqrt(MAX_SAMPLE_PIXELS / (img.width * img.height)));
      const width = Math.max(1, Math.round(img.width * scale));
      const height = Math.max(1, Math.round(img.height * scale));
      const canvas = document.createElement("canvas");
      canvas.width = width;
      canvas.height = height;
      const ctx = canvas.getContext("2d");
      if (!ctx) return null;
      ctx.drawImage(img, 0, 0, width, height);
      const data = ctx.getImageData(0, 0, width, height).data;
      let r = 0;
      let g2 = 0;
      let b2 = 0;
      let count = 0;
      for (let i2 = 0; i2 < data.length; i2 += 4) {
        r += data[i2];
        g2 += data[i2 + 1];
        b2 += data[i2 + 2];
        count++;
      }
      if (!count) return null;
      return `rgb(${Math.round(r / count)},${Math.round(g2 / count)},${Math.round(b2 / count)})`;
    } catch {
      return null;
    }
  }
  function loadImage(url, anonymous) {
    return new Promise((resolve) => {
      const img = new Image();
      if (anonymous) img.crossOrigin = "Anonymous";
      img.onload = () => resolve(img);
      img.onerror = () => resolve(null);
      img.src = url;
    });
  }
  async function getDominantColor(url) {
    if (!url) return null;
    if (/^https?:/i.test(url)) {
      const cors = await loadImage(url, true);
      if (cors) {
        const color = averageColorOf(cors);
        if (color) return color;
      }
    }
    const plain = await loadImage(url, false);
    return plain ? averageColorOf(plain) : null;
  }
  function hexToRgbString(hex) {
    const match = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
    if (!match) return null;
    const value = parseInt(match[1], 16);
    return `rgb(${value >> 16 & 255},${value >> 8 & 255},${value & 255})`;
  }
  async function getPaletteColor(uri) {
    if (!uri) return null;
    try {
      const palette = await Spicetify.colorExtractor?.(uri);
      for (const key of ["VIBRANT", "PROMINENT", "LIGHT_VIBRANT", "DESATURATED"]) {
        const hex = palette?.[key];
        if (typeof hex === "string" && hex) {
          const rgb = hexToRgbString(hex);
          if (rgb) return rgb;
        }
      }
    } catch {
    }
    return null;
  }
  async function resolveAccentColor(sourceUrl, fallbackUrl) {
    const direct = await getDominantColor(sourceUrl);
    if (direct) return direct;
    if (fallbackUrl && fallbackUrl !== sourceUrl) {
      const cover = await getDominantColor(fallbackUrl);
      if (cover) return cover;
    }
    const palette = await getPaletteColor(Spicetify.Player?.data?.item?.uri ?? null);
    if (palette) return palette;
    return FALLBACK_COLOR;
  }
  function enhanceColor(rgb, saturationBoost = 2, lightnessBoost = 1.3) {
    const parts = rgb.match(/\d+/g);
    if (!parts || parts.length < 3) return rgb;
    const [r, g2, b2] = parts.map(Number);
    const r1 = r / 255;
    const g1 = g2 / 255;
    const b1 = b2 / 255;
    const max = Math.max(r1, g1, b1);
    const min = Math.min(r1, g1, b1);
    let h2 = 0;
    let s2 = 0;
    let l = (max + min) / 2;
    if (max !== min) {
      const d2 = max - min;
      s2 = l > 0.5 ? d2 / (2 - max - min) : d2 / (max + min);
      switch (max) {
        case r1:
          h2 = (g1 - b1) / d2 + (g1 < b1 ? 6 : 0);
          break;
        case g1:
          h2 = (b1 - r1) / d2 + 2;
          break;
        case b1:
          h2 = (r1 - g1) / d2 + 4;
          break;
      }
      h2 /= 6;
    }
    s2 = Math.min(s2 * saturationBoost, 1);
    l = Math.min(l * lightnessBoost, 1);
    const hue2rgb = (p2, q2, t) => {
      if (t < 0) t += 1;
      if (t > 1) t -= 1;
      if (t < 1 / 6) return p2 + (q2 - p2) * 6 * t;
      if (t < 1 / 2) return q2;
      if (t < 2 / 3) return p2 + (q2 - p2) * (2 / 3 - t) * 6;
      return p2;
    };
    let r2;
    let g22;
    let b22;
    if (s2 === 0) {
      r2 = g22 = b22 = l;
    } else {
      const q2 = l < 0.5 ? l * (1 + s2) : l + s2 - l * s2;
      const p2 = 2 * l - q2;
      r2 = hue2rgb(p2, q2, h2 + 1 / 3);
      g22 = hue2rgb(p2, q2, h2);
      b22 = hue2rgb(p2, q2, h2 - 1 / 3);
    }
    return `rgb(${Math.round(r2 * 255)},${Math.round(g22 * 255)},${Math.round(b22 * 255)})`;
  }
  function readAccentBoosts() {
    return {
      satBoost: parseInt(localStorage.getItem("liquify-accent-sat-boost") || "17", 10) / 10,
      lightBoost: parseInt(localStorage.getItem("liquify-accent-light-boost") || "11", 10) / 10
    };
  }
  async function applyAccent(sourceUrl, fallbackUrl) {
    const { satBoost, lightBoost } = readAccentBoosts();
    const color = sourceUrl === UNSAMPLEABLE ? FALLBACK_COLOR : await resolveAccentColor(sourceUrl, fallbackUrl);
    document.documentElement.style.setProperty("--accent-color", enhanceColor(color, satBoost, lightBoost));
    window.dispatchEvent(new Event("liquifyAccentColorReady"));
  }
  var sleep2 = (ms) => new Promise((r) => setTimeout(r, ms));
  async function waitFor(get, timeoutMs) {
    const start2 = Date.now();
    for (; ; ) {
      const value = get();
      if (value) return value;
      if (Date.now() - start2 >= timeoutMs) return null;
      await sleep2(300);
    }
  }
  async function startBackground() {
    const root = await waitFor(() => document.querySelector(".Root__top-container"), 3e4);
    if (!root) return;
    const layerA = document.createElement("div");
    const layerB = document.createElement("div");
    layerA.classList.add("liquify-bg-layer", "layer-a");
    layerB.classList.add("liquify-bg-layer", "layer-b");
    root.prepend(layerA, layerB);
    const kawarp = new KawarpBackdrop();
    root.prepend(kawarp.el);
    applyKawarpAppearance();
    const animatedContainer = document.createElement("div");
    animatedContainer.classList.add("liquify-animated-bg");
    const animatedTilesA = [];
    const animatedTilesB = [];
    for (let i2 = 0; i2 < 2; i2++) {
      const tile = document.createElement("div");
      tile.classList.add("liquify-animated-tile");
      animatedContainer.appendChild(tile);
      animatedTilesA.push(tile);
    }
    for (let i2 = 0; i2 < 2; i2++) {
      const tile = document.createElement("div");
      tile.classList.add("liquify-animated-tile");
      animatedContainer.appendChild(tile);
      animatedTilesB.push(tile);
    }
    root.prepend(animatedContainer);
    const hideTiles = () => {
      animatedContainer.classList.remove("active");
      animatedTilesA.forEach((tile) => tile.classList.remove("active"));
      animatedTilesB.forEach((tile) => tile.classList.remove("active"));
    };
    let useAnimatedA = true;
    let useA = true;
    let lastAccentUrl = null;
    let lastRenderKey = null;
    const contextCoverCache = /* @__PURE__ */ new Map();
    let resolvingContextUri = null;
    function getContextUri() {
      const d2 = Spicetify.Player?.data || {};
      const state = Spicetify.Platform?.PlayerAPI?._state || {};
      return d2.context?.uri || d2.contextUri || d2.context_uri || state.context?.uri || state.contextUri || state.context_uri || "";
    }
    window.liquifyContextDebug = async () => {
      const cover = getCoverUrl();
      return {
        uri: getContextUri(),
        context: Spicetify.Player?.data?.context,
        rawCoverUrl: Spicetify.Player?.data?.item?.metadata?.image_url ?? null,
        coverUrl: cover,
        coverSampledColor: await getDominantColor(cover),
        paletteColor: await getPaletteColor(Spicetify.Player?.data?.item?.uri ?? null),
        accentSource: localStorage.getItem("liquify-accent-source") || "background"
      };
    };
    async function fetchPlaylistCover(uri) {
      const norm = (s2) => {
        const url = String(s2).replace("spotify:image:", "https://i.scdn.co/image/");
        return isHiResCoverOn() ? upgradeImageSize(url) : url;
      };
      try {
        const meta = await Spicetify.Platform?.PlaylistAPI?.getMetadata?.(uri);
        const img = meta?.images?.[0]?.url || meta?.picture || meta?.image;
        if (img) return norm(img);
      } catch {
      }
      try {
        const id = uri.split(":").pop();
        const res = await Spicetify.CosmosAsync?.get(
          `https://api.spotify.com/v1/playlists/${id}?fields=images`
        );
        const img = res?.images?.[0]?.url;
        if (img) return img;
      } catch {
      }
      return null;
    }
    function getResolvedContextCover() {
      const uri = getContextUri();
      if (!uri || !uri.includes(":playlist:")) return null;
      if (contextCoverCache.has(uri)) return contextCoverCache.get(uri) || null;
      if (resolvingContextUri !== uri) {
        resolvingContextUri = uri;
        fetchPlaylistCover(uri).then((img) => {
          resolvingContextUri = null;
          contextCoverCache.set(uri, img || "");
          if (img) window.dispatchEvent(new Event("liquifyBackgroundChange"));
        });
      }
      return null;
    }
    function render(kind, image, url) {
      if (!image) return;
      const engine = getBgEngine();
      const key = `${kind}|${engine}|${image}`;
      if (key === lastRenderKey) return;
      lastRenderKey = key;
      if (kind === "animated" && engine === "kawarp" && url) {
        layerA.classList.remove("active");
        layerB.classList.remove("active");
        hideTiles();
        kawarp.setActive(true);
        kawarp.show(url);
        return;
      }
      kawarp.setActive(false);
      if (kind === "animated") {
        layerA.classList.remove("active");
        layerB.classList.remove("active");
        animatedContainer.classList.add("active");
        const onTiles = useAnimatedA ? animatedTilesA : animatedTilesB;
        const offTiles = useAnimatedA ? animatedTilesB : animatedTilesA;
        onTiles.forEach((tile) => {
          tile.style.backgroundImage = image;
          tile.classList.add("active");
        });
        offTiles.forEach((tile) => tile.classList.remove("active"));
        useAnimatedA = !useAnimatedA;
        return;
      }
      hideTiles();
      if (useA) {
        layerA.style.backgroundImage = image;
        layerA.classList.add("active");
        layerB.classList.remove("active");
      } else {
        layerB.style.backgroundImage = image;
        layerB.classList.add("active");
        layerA.classList.remove("active");
      }
      useA = !useA;
    }
    function resolveBackdrop() {
      const bgMode = localStorage.getItem("liquify-bg-mode") || "dynamic";
      const customImage = getLibraryUrl();
      const bgUrl = localStorage.getItem("liquify-bg-url");
      const customAnimated = localStorage.getItem("liquify-bg-custom-animated") === "on";
      const customKind = customAnimated ? "animated" : "static";
      const coverUrl = getCoverUrl();
      const from = (kind, url) => ({ kind, image: url ? cssImage(url) : null, url, sampleUrl: url });
      let result;
      if (bgMode === "custom" && customImage) result = from(customKind, customImage);
      else if (bgMode === "url" && bgUrl) {
        result = { kind: customKind, image: cssImage(bgUrl), url: bgUrl, sampleUrl: UNSAMPLEABLE };
      } else if (bgMode === "playlist") {
        const playlistCover = getResolvedContextCover();
        const isPlaylist = getContextUri().includes(":playlist:");
        result = from(customKind, isPlaylist ? playlistCover : playlistCover || coverUrl);
      } else if (bgMode === "animated") result = from("animated", coverUrl);
      else result = from("static", coverUrl);
      if (result.kind === "animated" && getBgEngine() === "kawarp" && !kawarp.canRender(result.url)) {
        result = { ...result, kind: "static" };
      }
      if (!result.image && lastRenderKey === null) {
        return { kind: "static", image: EMPTY_BACKDROP, url: null, sampleUrl: null };
      }
      return result;
    }
    function accentSourceOf(sampleUrl) {
      const source = localStorage.getItem("liquify-accent-source") || "background";
      if (source === "cover") return getCoverUrl();
      if (sampleUrl === UNSAMPLEABLE) return UNSAMPLEABLE;
      return sampleUrl || getCoverUrl();
    }
    async function updateBackgroundAndAccent() {
      const { kind, image, url, sampleUrl } = resolveBackdrop();
      render(kind, image, url);
      const accentUrl = accentSourceOf(sampleUrl);
      if (accentUrl && accentUrl !== lastAccentUrl) {
        lastAccentUrl = accentUrl;
        await applyAccent(accentUrl, getCoverUrl());
      }
    }
    async function updateAccentOnly() {
      const accentUrl = accentSourceOf(resolveBackdrop().sampleUrl);
      if (!accentUrl) return;
      lastAccentUrl = accentUrl;
      await applyAccent(accentUrl, getCoverUrl());
    }
    updateBackgroundAndAccent();
    window.addEventListener("liquifyBackgroundChange", updateBackgroundAndAccent);
    window.addEventListener("liquifyBackgroundChange", () => {
      applyKawarpAppearance();
      kawarp.applyOptions();
    });
    window.addEventListener("liquifyAccentColorParamsChange", updateAccentOnly);
    waitFor(
      () => typeof Spicetify?.Player?.addEventListener === "function" ? Spicetify.Player : null,
      3e4
    ).then((player) => {
      try {
        player?.addEventListener("son…122970 tokens truncated…RADIUS_DEFAULTS.right));
    const [configText, setConfigText] = React.useState(() => exportConfig());
    const [configStatus, setConfigStatus] = React.useState(null);
    const [configDirty, setConfigDirty] = React.useState(false);
    const configFingerprint = React.useRef(settingsFingerprint());
    const unixLike = isUnixLikeOS();
    const artistFileRef = React.useRef(null);
    const [libraryOpen, setLibraryOpen] = React.useState(false);
    const cfg = t.config || {};
    const handleConfigCopy = async () => {
      try {
        await navigator.clipboard.writeText(configText);
        setConfigStatus({ ok: true, msg: cfg.copied || "Copied to clipboard." });
      } catch {
        try {
          const ta = document.createElement("textarea");
          ta.value = configText;
          ta.style.position = "fixed";
          ta.style.opacity = "0";
          document.body.appendChild(ta);
          ta.select();
          document.execCommand("copy");
          ta.remove();
          setConfigStatus({ ok: true, msg: cfg.copied || "Copied to clipboard." });
        } catch {
          setConfigStatus({ ok: false, msg: cfg.copyFailed || "Couldn't copy \u2014 select the text and copy manually." });
        }
      }
    };
    React.useEffect(() => {
      if (configDirty) return;
      const sync = () => {
        const fingerprint = settingsFingerprint();
        if (fingerprint === configFingerprint.current) return;
        configFingerprint.current = fingerprint;
        setConfigText(exportConfig());
        setConfigStatus(null);
      };
      sync();
      const id = setInterval(sync, 500);
      return () => clearInterval(id);
    }, [configDirty]);
    const handleConfigApply = async () => {
      let text = configText;
      try {
        const clip = await navigator.clipboard.readText();
        if (clip && clip.trim()) {
          text = clip;
          setConfigText(clip);
        }
      } catch {
      }
      const res = importConfig(text);
      if (!res.ok) setConfigStatus({ ok: false, msg: res.error || cfg.invalid || "Invalid config." });
    };
    React.useEffect(() => {
      ensureSettingsUiStyle();
    }, []);
    React.useEffect(() => {
      const handler = () => {
        setPlaybarCoverRadius(readNum(PLAYBAR_COVER_BORDER_RADIUS_KEY, PLAYBAR_COVER_DEFAULTS.borderRadius));
      };
      window.addEventListener("liquifyPlaybarCoverRadiusChange", handler);
      return () => window.removeEventListener("liquifyPlaybarCoverRadiusChange", handler);
    }, []);
    const applyAccentMode = (mode) => {
      setAccentMode(mode);
      if (mode === "custom") {
        applyAccent2(accentColor);
      } else if (mode === "dynamic") {
        resetDynamicAccentCache();
        applyDynamicAccent();
      } else {
        resetAccentToDefault();
      }
    };
    const applyAccentSource = (source) => {
      setAccentSource(source);
      localStorage.setItem("liquify-accent-source", source);
      window.dispatchEvent(new Event("liquifyAccentColorParamsChange"));
    };
    const applyGlowMode = (mode) => {
      setGlowMode(mode);
      if (mode === "custom") applyGlowAccent(glowColor);
      else resetGlowAccentToDefault();
    };
    const applyBgMode = async (mode) => {
      setBgMode(mode);
      localStorage.setItem("liquify-bg-mode", mode);
      if (mode === "custom" && !getLibraryUrl()) setLibraryOpen(true);
      if (mode === "url") {
        const saved = localStorage.getItem("liquify-bg-url");
        if (!saved) return;
      }
      updateBackground();
    };
    const applyArtistMode = async (mode) => {
      setArtistBgMode(mode);
      localStorage.setItem("liquify-artist-bg-mode", mode);
      if (mode === "custom") {
        const saved = localStorage.getItem("liquify-artist-bg-image");
        if (!saved) {
          artistFileRef.current?.click();
          return;
        }
      }
      if (mode === "url") {
        const saved = localStorage.getItem("liquify-artist-bg-url");
        if (!saved) return;
      }
      props.artistCtrl?.setMode?.(mode);
    };
    const applyPlayerWidthMode = (mode) => {
      setPlayerWidthMode(mode);
      localStorage.setItem("liquify-player-width", mode);
      applyPlayerWidth(mode);
    };
    const applyPlayerCustom = (nextW, nextH) => {
      localStorage.setItem("liquify-player-custom-width", String(nextW));
      localStorage.setItem("liquify-player-custom-height", String(nextH));
      applyPlayerWidth("custom");
    };
    const applyRadius = (value) => {
      setPlayerRadiusState(value);
      applyPlayerRadius(value);
    };
    const applyPlaylistHeaderMode = (mode) => {
      setPlaylistHeader(mode);
      applyPlaylistHeader(mode);
    };
    const applyActionBarBoxMode = (mode) => {
      setActionBarBox(mode);
      applyActionBarBox(mode);
    };
    const applyAppearance = (surface, field, value) => {
      setBgAppearance((prev) => ({
        ...prev,
        [surface]: { ...prev[surface], [field]: value }
      }));
      setBackgroundAppearance(surface, field, value);
    };
    const appearanceRows = /* @__PURE__ */ React.createElement(React.Fragment, null, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.backgroundBlur), /* @__PURE__ */ React.createElement(
      Stepper,
      {
        value: bgAppearance[bgSurface].blur,
        min: 0,
        max: BG_BLUR_RANGE[bgSurface],
        onChange: (v2) => applyAppearance(bgSurface, "blur", v2)
      }
    )), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.backgroundBrightness || "Background Brightness:"), /* @__PURE__ */ React.createElement(
      Stepper,
      {
        value: bgAppearance[bgSurface].brightness,
        min: 0,
        max: 200,
        onChange: (v2) => applyAppearance(bgSurface, "brightness", v2)
      }
    )));
    const applyArtistBlur = (value) => {
      setArtistScrollBlur(value);
      applyArtistScrollEffect(value, artistScrollBrightness);
    };
    const applyArtistBrightness = (value) => {
      setArtistScrollBrightness(value);
      applyArtistScrollEffect(artistScrollBlur, value);
    };
    const applyTransparent = (w2, h2) => {
      setTcW(w2);
      setTcH(h2);
      applyTransparentControls(w2, h2);
    };
    const handleReset = () => {
      localStorage.setItem("liquify-accent-mode", "dynamic");
      localStorage.removeItem("liquify-custom-color");
      localStorage.setItem("liquify-accent-sat-boost", "17");
      localStorage.setItem("liquify-accent-light-boost", "11");
      localStorage.setItem("liquify-accent-source", "background");
      setAccentMode("dynamic");
      setAccentSource("background");
      setAccentColor("#1DB954");
      setAccentSatBoost(17);
      setAccentLightBoost(11);
      resetDynamicAccentCache();
      applyDynamicAccent();
      window.dispatchEvent(new Event("liquifyAccentColorParamsChange"));
      localStorage.setItem("liquify-glow-mode", "default");
      localStorage.removeItem("liquify-glow-color");
      setGlowMode("default");
      setGlowColor("#1DB954");
      resetGlowAccentToDefault();
      localStorage.setItem("liquify-bg-mode", "dynamic");
      localStorage.removeItem("liquify-bg-url");
      localStorage.setItem("liquify-bg-custom-animated", "off");
      setBgCustomAnimated("off");
      resetKawarpDefaults();
      setKawarpState({ ...KAWARP_DEFAULTS });
      resetBackgroundAppearance();
      setBgAppearance(readAllBackgroundAppearance());
      setBgEngineState("kawarp");
      setBgMode("dynamic");
      setBgUrl("");
      window.dispatchEvent(new Event("liquifyBackgroundChange"));
      localStorage.setItem("liquify-artist-bg-mode", "theme");
      localStorage.setItem("liquify-artist-scroll-blur", "15");
      localStorage.setItem("liquify-artist-scroll-brightness", "70");
      localStorage.removeItem("liquify-artist-bg-url");
      setArtistBgMode("theme");
      setArtistScrollBlur(15);
      setArtistScrollBrightness(70);
      setArtistBgUrl("");
      applyArtistScrollEffect(15, 70);
      props.artistCtrl?.setMode?.("theme");
      localStorage.setItem("liquify-player-width", "theme");
      localStorage.setItem("liquify-player-custom-width", String(DEFAULT_CUSTOM_WIDTH));
      localStorage.setItem("liquify-player-custom-height", String(DEFAULT_CUSTOM_HEIGHT));
      localStorage.setItem("liquify-player-radius", "30");
      setPlayerWidthMode("theme");
      setPlayerCustomW(DEFAULT_CUSTOM_WIDTH);
      setPlayerCustomH(DEFAULT_CUSTOM_HEIGHT);
      setPlayerRadiusState(30);
      applyPlayerWidth("theme");
      applyPlayerRadius(30);
      localStorage.setItem("liquify-playlist-header-mode", "show");
      setPlaylistHeader("show");
      applyPlaylistHeader("show");
      localStorage.setItem("liquify-action-bar-box-mode", "show");
      setActionBarBox("show");
      applyActionBarBox("show");
      localStorage.setItem("liquify-tc-width", "135");
      localStorage.setItem("liquify-tc-height", "64");
      setTcW(135);
      setTcH(64);
      applyTransparentControls(135, 64);
      localStorage.setItem(PLAYBAR_COVER_BORDER_RADIUS_KEY, String(PLAYBAR_COVER_DEFAULTS.borderRadius));
      setPlaybarCoverRadius(PLAYBAR_COVER_DEFAULTS.borderRadius);
      applyPlaybarCoverBorderRadius(PLAYBAR_COVER_DEFAULTS.borderRadius);
      window.dispatchEvent(new Event("liquifyPlaybarCoverRadiusChange"));
      setProgressBarHeightState(PROGRESS_BAR_HEIGHT_DEFAULT);
      setProgressBarHeight(PROGRESS_BAR_HEIGHT_DEFAULT);
      setProgressBarRadiusState(PROGRESS_BAR_RADIUS_DEFAULT);
      setProgressBarRadius(PROGRESS_BAR_RADIUS_DEFAULT);
      setProgressBarCompatState(false);
      setProgressBarCompat(false, ensureProgressBarRadiusApplied);
      setTransparentPlayer("off");
      applyTransparentPlayer("off");
      setFloatingPlayer("off");
      applyFloatingPlayer("off");
      setConnectBar("show");
      applyConnectBar("show");
      setCompactPlayer("off");
      applyCompactPlayer("off");
      setPlayerIcons("on");
      setPlayerControlIcons("on");
      localStorage.setItem(CCA_ENABLED_KEY, CCA_DEFAULTS.enabled);
      localStorage.setItem(CCA_WIDTH_KEY, String(CCA_DEFAULTS.width));
      localStorage.setItem(CCA_HEIGHT_KEY, String(CCA_DEFAULTS.height));
      localStorage.setItem(CCA_MARGIN_BOTTOM_KEY, String(CCA_DEFAULTS.marginBottom));
      localStorage.setItem(CCA_MARGIN_LEFT_KEY, String(CCA_DEFAULTS.marginLeft));
      setCcaEnabled(CCA_DEFAULTS.enabled);
      setCcaWidth(CCA_DEFAULTS.width);
      setCcaHeight(CCA_DEFAULTS.height);
      setCcaMarginBottom(CCA_DEFAULTS.marginBottom);
      setCcaMarginLeft(CCA_DEFAULTS.marginLeft);
      applyComfyCoverArt();
      localStorage.setItem(NPVC_MODE_KEY, NPVC_DEFAULTS.mode);
      localStorage.setItem(NPVC_SHOW_ALWAYS_KEY, NPVC_DEFAULTS.showAlways);
      localStorage.setItem(NPVC_BLUR_KEY, String(NPVC_DEFAULTS.blur));
      setNpvcMode(NPVC_DEFAULTS.mode);
      setNpvcShowAlways(NPVC_DEFAULTS.showAlways);
      setNpvcBlur(NPVC_DEFAULTS.blur);
      window.dispatchEvent(new Event("liquifyNpvcUpdate"));
      localStorage.setItem(NSC_SHOW_KEY, NSC_DEFAULTS.show);
      localStorage.setItem(NSC_POSITION_KEY, NSC_DEFAULTS.position);
      localStorage.setItem(NSC_HEIGHT_KEY, String(NSC_DEFAULTS.height));
      localStorage.setItem(NSC_MAX_WIDTH_KEY, String(NSC_DEFAULTS.maxWidth));
      localStorage.setItem(NSC_GAP_KEY, String(NSC_DEFAULTS.gap));
      localStorage.setItem(NSC_COVER_SIZE_KEY, String(NSC_DEFAULTS.coverSize));
      localStorage.setItem(NSC_HPAD_KEY, String(NSC_DEFAULTS.hPad));
      localStorage.setItem(NSC_VPAD_KEY, String(NSC_DEFAULTS.vPad));
      localStorage.setItem(NSC_GAP_PLAYER_KEY, String(NSC_DEFAULTS.gapToPlayer));
      localStorage.setItem(NSC_BORDER_RADIUS_KEY, String(NSC_DEFAULTS.borderRadius));
      localStorage.setItem(NSC_COVER_BORDER_RADIUS_KEY, String(NSC_DEFAULTS.coverBorderRadius));
      setNscShow(NSC_DEFAULTS.show);
      setNscPosition(NSC_DEFAULTS.position);
      setNscHeight(NSC_DEFAULTS.height);
      setNscMaxWidth(NSC_DEFAULTS.maxWidth);
      setNscGap(NSC_DEFAULTS.gap);
      setNscCoverSize(NSC_DEFAULTS.coverSize);
      setNscHPad(NSC_DEFAULTS.hPad);
      setNscVPad(NSC_DEFAULTS.vPad);
      setNscGapToPlayer(NSC_DEFAULTS.gapToPlayer);
      setNscBorderRadius(NSC_DEFAULTS.borderRadius);
      setNscCoverBorderRadius(NSC_DEFAULTS.coverBorderRadius);
      window.dispatchEvent(new Event("liquifyNscUpdate"));
      localStorage.setItem("liquify-lyrics-mode", "romanization");
      setLyricsMode("romanization");
      window.dispatchEvent(new Event("liquifyLyricsModeChange"));
      setThemedLyricsState("on");
      setThemedLyrics(true);
      setLyricsFontSizeState(LYRICS_FONT_SIZE_DEFAULT);
      setLyricsFontSize(LYRICS_FONT_SIZE_DEFAULT);
      setLyricsMarginState(LYRICS_MARGIN_DEFAULT);
      setLyricsMargin(LYRICS_MARGIN_DEFAULT);
      localStorage.setItem(POPUP_BOUNCE_KEY, "on");
      setPopupBounceMode("on");
      applyPopupBounce("on");
      setHomeLayout("on");
      applyHomeLayout("on");
      setPerformanceMode(false);
      setGlassEnabled(true);
      setGlassBlurState(GLASS_BLUR_DEFAULT);
      setGlassBlur(GLASS_BLUR_DEFAULT);
      setBackdropBlurState(BACKDROP_BLUR_DEFAULT);
      setBackdropBlur(BACKDROP_BLUR_DEFAULT);
      setNavRadiusState(LAYOUT_RADIUS_DEFAULTS.nav);
      setNavRadius(LAYOUT_RADIUS_DEFAULTS.nav);
      setMainRadiusState(LAYOUT_RADIUS_DEFAULTS.main);
      setMainRadius(LAYOUT_RADIUS_DEFAULTS.main);
      setRightRadiusState(LAYOUT_RADIUS_DEFAULTS.right);
      setRightRadius(LAYOUT_RADIUS_DEFAULTS.right);
      resetFonts();
      setBodyFontState(FONT_DEFAULT);
      setHeadingFontState(FONT_DEFAULT);
      resetVinyl();
      setVinylState({ npv: false, playbar: false, cinema: false });
      setVinylSpeedState(VINYL_SPEED_DEFAULT);
      resetSidebarBlur();
      setSidebarBlurState({
        left: { on: false, amount: SIDEBAR_BLUR_DEFAULT },
        right: { on: false, amount: SIDEBAR_BLUR_DEFAULT }
      });
      setLocalFilesTransparentState("off");
      setLocalFilesTransparent("off");
    };
    return /* @__PURE__ */ React.createElement("div", { className: "liquifySettingsPanel" }, /* @__PURE__ */ React.createElement("div", { className: "liquifySettingsHeader" }, /* @__PURE__ */ React.createElement("h3", { className: "liquifySettingsTitle" }, t.title), /* @__PURE__ */ React.createElement("div", { className: "liquifyHeaderActions" }, /* @__PURE__ */ React.createElement(ButtonTooltip, { text: "Discord" }, /* @__PURE__ */ React.createElement(
      "button",
      {
        type: "button",
        className: "liquifyControlSurface liquifyHeaderActionBtn",
        "aria-label": "Discord",
        onClick: () => openExternalLink(LIQUIFY_DISCORD_URL)
      },
      getDiscordIcon()
    )), /* @__PURE__ */ React.createElement(ButtonTooltip, { text: "GitHub" }, /* @__PURE__ */ React.createElement(
      "button",
      {
        type: "button",
        className: "liquifyControlSurface liquifyHeaderActionBtn",
        "aria-label": "GitHub",
        onClick: () => openExternalLink(LIQUIFY_GITHUB_URL)
      },
      getGithubIcon()
    )), /* @__PURE__ */ React.createElement(ButtonTooltip, { text: t.close || "Close" }, /* @__PURE__ */ React.createElement(
      "button",
      {
        type: "button",
        className: "liquifyControlSurface liquifyHeaderActionBtn liquifyCloseBtn",
        "aria-label": t.close || "Close",
        onClick: props.onClose
      },
      /* @__PURE__ */ React.createElement("svg", { viewBox: "0 0 24 24", "aria-hidden": "true" }, /* @__PURE__ */ React.createElement("path", { d: "M5 5 19 19" }), /* @__PURE__ */ React.createElement("path", { d: "M19 5 5 19" }))
    )))), /* @__PURE__ */ React.createElement("div", { className: "liquifySearchIsland" }, /* @__PURE__ */ React.createElement(
      "input",
      {
        type: "text",
        className: "liquifyControlSurface liquifySearchInput",
        placeholder: t.searchPlaceholder || "Search settings...",
        value: searchQuery,
        onChange: (e) => setSearchQuery(e.target.value),
        spellCheck: false
      }
    ), /* @__PURE__ */ React.createElement("div", { className: "liquifySectionNavWrap" }, /* @__PURE__ */ React.createElement("div", { className: "liquifySectionNav", ref: sectionNavRef }, [
      { id: "language", title: titles.language || "Language" },
      { id: "accent", title: titles.accent },
      { id: "background", title: titles.background },
      { id: "artist", title: titles.artist },
      { id: "ui", title: titles.ui || "UI" },
      { id: "player", title: titles.player },
      { id: "nextSongCard", title: titles.nextSongCard || "Next Song Card" },
      { id: "canvasCoverArt", title: titles.canvasCoverArt || "Canvas Cover Art" },
      { id: "playlist", title: titles.playlist },
      { id: "lyrics", title: titles.lyrics || "Lyrics" },
      { id: "transparent", title: titles.transparent },
      { id: "config", title: titles.config || "Config" }
    ].map((s2) => /* @__PURE__ */ React.createElement(
      "button",
      {
        key: s2.id,
        type: "button",
        className: "liquifySectionNavBtn",
        onClick: () => jumpToSection(s2.id)
      },
      s2.title
    ))))), sectionNavScrollControls, /* @__PURE__ */ React.createElement("div", { className: "liquifySettingsBody", ref: bodyRef }, /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-language", title: titles.language || "Language" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.language || "Language:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.language })), /* @__PURE__ */ React.createElement(
      Select,
      {
        value: languageMode === "auto" ? "auto" : "custom",
        options: [
          { value: "auto", label: t.languageOptions?.auto || "Follow Spotify" },
          { value: "custom", label: t.dropdown.custom }
        ],
        onChange: (v2) => {
          const next = v2 === "auto" ? "auto" : languageCode;
          setLanguageModeState(next);
          setLanguage(next);
        }
      }
    )), languageMode !== "auto" && /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.languageChoice || "Language:"), /* @__PURE__ */ React.createElement(
      Select,
      {
        value: languageMode,
        options: getAvailableLanguages(),
        onChange: (v2) => {
          setLanguageModeState(v2);
          setLanguage(v2);
        }
      }
    ))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-accent", title: titles.accent }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.accentColor, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.accentColor })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRowControls" }, /* @__PURE__ */ React.createElement(
      Select,
      {
        value: accentMode,
        options: [
          { value: "default", label: t.dropdown.default },
          { value: "custom", label: t.dropdown.custom },
          { value: "dynamic", label: t.dropdown.dynamic }
        ],
        onChange: applyAccentMode
      }
    ), accentMode === "custom" && /* @__PURE__ */ React.createElement(
      ColorPicker,
      {
        value: accentColor,
        onChange: (next) => {
          setAccentColor(next);
          localStorage.setItem("liquify-custom-color", next);
          applyAccent2(next);
        }
      }
    ))), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.accentSource, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.accentSource })), /* @__PURE__ */ React.createElement(
      Select,
      {
        value: accentSource,
        options: [
          { value: "background", label: t.dropdown.backgroundSource },
          { value: "cover", label: t.dropdown.songCover }
        ],
        onChange: applyAccentSource
      }
    )), accentMode === "dynamic" && /* @__PURE__ */ React.createElement(React.Fragment, null, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.accentSatBoost, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.accentSatBoost })), /* @__PURE__ */ React.createElement(Stepper, { value: accentSatBoost, min: 1, max: 100, onChange: (v2) => {
      setAccentSatBoost(v2);
      localStorage.setItem("liquify-accent-sat-boost", String(v2));
      window.dispatchEvent(new Event("liquifyAccentColorParamsChange"));
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.accentLightBoost, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.accentLightBoost })), /* @__PURE__ */ React.createElement(Stepper, { value: accentLightBoost, min: 1, max: 100, onChange: (v2) => {
      setAccentLightBoost(v2);
      localStorage.setItem("liquify-accent-light-boost", String(v2));
      window.dispatchEvent(new Event("liquifyAccentColorParamsChange"));
    } })))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-background", title: titles.background }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.background, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.background })), /* @__PURE__ */ React.createElement("div", { className: "liquifyStackedControls" }, /* @__PURE__ */ React.createElement(
      Select,
      {
        value: bgMode,
        options: [
          { value: "dynamic", label: t.dropdown.dynamic },
          { value: "animated", label: t.dropdown.animated },
          { value: "playlist", label: t.dropdown.playlist || "Playlist" },
          { value: "custom", label: t.dropdown.custom },
          { value: "url", label: t.dropdown.url || "URL" }
        ],
        onChange: (m2) => void applyBgMode(m2)
      }
    ), bgMode === "custom" && /* @__PURE__ */ React.createElement(
      "button",
      {
        type: "button",
        className: "liquifyControlSurface liquifyActionBtn",
        onClick: () => setLibraryOpen(true)
      },
      t.openLibrary || "Image Library"
    ), bgMode === "url" && /* @__PURE__ */ React.createElement(
      "input",
      {
        type: "text",
        className: "liquifyControlSurface liquifyTextInput",
        placeholder: t.enterUrl || "Enter image URL...",
        value: bgUrl,
        onChange: (e) => {
          const val = e.target.value;
          setBgUrl(val);
          localStorage.setItem("liquify-bg-url", val);
          if (val) updateBackground();
        }
      }
    ))), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.hiResCover || "Use hi-res pictures:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.hiResCover })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: hiResCover,
        onChange: (checked) => {
          setHiResCoverState(checked);
          setHiResCover(checked);
        }
      }
    )), (bgMode === "custom" || bgMode === "url" || bgMode === "playlist") && /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.animatedBackground || "Animated Background:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.animatedBackground })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: bgCustomAnimated === "on",
        onChange: (checked) => {
          const v2 = checked ? "on" : "off";
          setBgCustomAnimated(v2);
          localStorage.setItem("liquify-bg-custom-animated", v2);
          window.dispatchEvent(new Event("liquifyBackgroundChange"));
        }
      }
    )), !animatedActive && appearanceRows, animatedActive && /* @__PURE__ */ React.createElement(SubSection, { title: sub.kawarp || "Animated Background" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.animatedEngine || "Engine:"), /* @__PURE__ */ React.createElement(
      Select,
      {
        value: bgEngine,
        onChange: (v2) => {
          setBgEngineState(v2);
          setBgEngine(v2);
        },
        options: [
          { value: "kawarp", label: t.dropdown.engineKawarp || "Kawarp (WebGL)" },
          { value: "tiles", label: t.dropdown.engineTiles || "Classic" }
        ]
      }
    )), appearanceRows, bgEngine === "kawarp" && Object.keys(KAWARP_RANGES).map((key) => /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", key }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, (t.kawarp || {})[key] || key), /* @__PURE__ */ React.createElement(
      Stepper,
      {
        value: kawarp[key],
        min: KAWARP_RANGES[key].min,
        max: KAWARP_RANGES[key].max,
        onChange: (v2) => applyKawarp(key, v2)
      }
    )))), /* @__PURE__ */ React.createElement(
      BackgroundLibrary,
      {
        open: libraryOpen,
        onClose: () => setLibraryOpen(false),
        labels: {
          title: t.imageLibrary || "Image Library",
          add: t.addImages || "Add images",
          empty: t.libraryEmpty || "No images yet. Add some to get started.",
          remove: t.removeImage || "Remove",
          close: t.close || "Close"
        }
      }
    )), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-artist", title: titles.artist }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.apbackground, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.artistBackground })), /* @__PURE__ */ React.createElement("div", { className: "liquifyStackedControls" }, /* @__PURE__ */ React.createElement(
      Select,
      {
        value: artistBgMode,
        options: [
          { value: "theme", label: t.dropdown.theme },
          { value: "none", label: t.dropdown.none },
          { value: "custom", label: t.dropdown.custom },
          { value: "url", label: t.dropdown.url || "URL" }
        ],
        onChange: (m2) => void applyArtistMode(m2)
      }
    ), artistBgMode === "custom" && /* @__PURE__ */ React.createElement(React.Fragment, null, /* @__PURE__ */ React.createElement(
      "button",
      {
        type: "button",
        className: "liquifyControlSurface liquifyActionBtn",
        onClick: () => artistFileRef.current?.click()
      },
      chooseFileLabel
    ), /* @__PURE__ */ React.createElement(
      "input",
      {
        ref: artistFileRef,
        type: "file",
        accept: "image/*",
        style: { display: "none" },
        onChange: async (e) => {
          const file = e.target.files?.[0];
          if (!file) return;
          await applyCustomArtistBackground(file);
          props.artistCtrl?.applySavedModeIfArtist?.();
        }
      }
    )), artistBgMode === "url" && /* @__PURE__ */ React.createElement(
      "input",
      {
        type: "text",
        className: "liquifyControlSurface liquifyTextInput",
        placeholder: t.enterUrl || "Enter image URL...",
        value: artistBgUrl,
        onChange: (e) => {
          const val = e.target.value;
          setArtistBgUrl(val);
          localStorage.setItem("liquify-artist-bg-url", val);
          if (val) props.artistCtrl?.setMode?.("url");
        }
      }
    ))), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.artistScrollBlur, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.artistScrollBlur })), /* @__PURE__ */ React.createElement(Stepper, { value: artistScrollBlur, min: 0, max: 100, onChange: applyArtistBlur })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.artistScrollBrightness, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.artistScrollBrightness })), /* @__PURE__ */ React.createElement(Stepper, { value: artistScrollBrightness, min: 0, max: 200, onChange: applyArtistBrightness }))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-ui", title: titles.ui || "UI" }, /* @__PURE__ */ React.createElement(SubSection, { title: sub.performanceGlass || "Performance & Glass" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.performanceMode || "Performance Mode:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.performanceMode })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: performanceMode,
        onChange: (checked) => {
          setPerformanceMode(checked);
          setGlassEnabled(!checked);
        }
      }
    )), performanceMode ? /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.backdropBlur || "Backdrop Blur (px):"), /* @__PURE__ */ React.createElement(Stepper, { value: backdropBlur, min: 0, max: 80, onChange: (v2) => {
      setBackdropBlurState(v2);
      setBackdropBlur(v2);
    } })) : /* @__PURE__ */ React.createElement(React.Fragment, null, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.glassBlur || "Glass Blur (px):", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.glassBlur })), /* @__PURE__ */ React.createElement(Stepper, { value: glassBlur, min: 0, max: 30, onChange: (v2) => {
      setGlassBlurState(v2);
      setGlassBlur(v2);
    } })))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.animations || "Animations" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.popupBounce || "Popup Bounce:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.popupBounce })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: popupBounceMode === "on",
        onChange: (checked) => {
          const v2 = checked ? "on" : "off";
          setPopupBounceMode(v2);
          applyPopupBounce(v2);
        }
      }
    ))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.homescreen || "Homescreen" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.newHomescreenLayout || "Use New Homescreen Layout:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.newHomescreenLayout })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: homeLayout === "on",
        onChange: (checked) => {
          const v2 = checked ? "on" : "off";
          setHomeLayout(v2);
          applyHomeLayout(v2);
        }
      }
    ))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.borderRadius || "Border Radius" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.leftSidebarRadius || "Left Sidebar Border Radius:"), /* @__PURE__ */ React.createElement(Stepper, { value: navRadius, min: 0, max: 50, onChange: (v2) => {
      setNavRadiusState(v2);
      setNavRadius(v2);
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.mainViewRadius || "Main View Border Radius:"), /* @__PURE__ */ React.createElement(Stepper, { value: mainRadius, min: 0, max: 50, onChange: (v2) => {
      setMainRadiusState(v2);
      setMainRadius(v2);
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.rightSidebarRadius || "Right Sidebar Border Radius:"), /* @__PURE__ */ React.createElement(Stepper, { value: rightRadius, min: 0, max: 50, onChange: (v2) => {
      setRightRadiusState(v2);
      setRightRadius(v2);
    } }))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.sidebars || "Sidebars" }, Object.keys(SIDEBARS).map((side) => /* @__PURE__ */ React.createElement(React.Fragment, { key: side }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.[side === "left" ? "leftSidebarBlur" : "rightSidebarBlur"] || (side === "left" ? "Blur Behind Left Sidebar:" : "Blur Behind Right Sidebar:"), /* @__PURE__ */ React.createElement(HelpTip, { text: tips.sidebarBlur })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: sidebarBlur[side].on,
        onChange: (checked) => applySidebarBlur(side, { on: checked })
      }
    )), sidebarBlur[side].on && /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.[side === "left" ? "leftSidebarBlurAmount" : "rightSidebarBlurAmount"] || (side === "left" ? "Left Sidebar Blur (px):" : "Right Sidebar Blur (px):")), /* @__PURE__ */ React.createElement(
      Stepper,
      {
        value: sidebarBlur[side].amount,
        min: 0,
        max: 80,
        onChange: (v2) => applySidebarBlur(side, { amount: v2 })
      }
    )))), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.localFilesTransparent || "Transparent Local Files Card:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.localFilesTransparent })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: localFilesTransparent === "on",
        onChange: (checked) => {
          const v2 = checked ? "on" : "off";
          setLocalFilesTransparentState(v2);
          setLocalFilesTransparent(v2);
        }
      }
    ))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.typography || "Typography" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.bodyFont || "Body Font:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.bodyFont })), /* @__PURE__ */ React.createElement(FontPicker, { value: bodyFont, onChange: (v2) => {
      setBodyFontState(v2);
      setFont("body", v2);
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.ui?.headingFont || "Heading Font:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.headingFont })), /* @__PURE__ */ React.createElement(FontPicker, { value: headingFont, onChange: (v2) => {
      setHeadingFontState(v2);
      setFont("heading", v2);
    } }))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.vinyl || "Vinyl Cover Art" }, Object.keys(VINYL_SURFACES).map((surface) => /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", key: surface }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, (t.vinyl || {})[surface] || surface, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.vinyl })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: vinyl[surface],
        onChange: (checked) => applyVinyl(surface, checked)
      }
    ))), Object.keys(VINYL_SURFACES).some((s2) => vinyl[s2]) && /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, (t.vinyl || {}).speed || "Seconds Per Turn:"), /* @__PURE__ */ React.createElement(
      Stepper,
      {
        value: vinylSpeed,
        min: 1,
        max: 60,
        onChange: (v2) => {
          setVinylSpeedState(v2);
          setVinylSpeed(v2);
        }
      }
    )))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-player", title: titles.player }, /* @__PURE__ */ React.createElement(SubSection, { title: sub.sizeShape || "Size & Shape" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.playerWidth, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.playerWidth })), /* @__PURE__ */ React.createElement(
      Select,
      {
        value: playerWidthMode,
        options: [
          { value: "default", label: t.dropdown.default },
          { value: "theme", label: t.dropdown.theme },
          { value: "custom", label: t.dropdown.custom }
        ],
        onChange: applyPlayerWidthMode
      }
    )), playerWidthMode === "custom" && /* @__PURE__ */ React.createElement("div", { className: "liquifySubBlock" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.playerCustomWidth), /* @__PURE__ */ React.createElement(
      Stepper,
      {
        value: playerCustomW,
        min: 0,
        max: 100,
        onChange: (v2) => {
          setPlayerCustomW(v2);
          applyPlayerCustom(v2, playerCustomH);
        }
      }
    )), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.playerCustomHeight), /* @__PURE__ */ React.createElement(
      Stepper,
      {
        value: playerCustomH,
        min: 0,
        max: 300,
        onChange: (v2) => {
          setPlayerCustomH(v2);
          applyPlayerCustom(playerCustomW, v2);
        }
      }
    ))), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.playerRadius), /* @__PURE__ */ React.createElement(Stepper, { value: playerRadius, min: 0, max: 100, onChange: applyRadius }))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.progressVolume || "Progress & Volume Bar" }, !progressBarCompat && /* @__PURE__ */ React.createElement(React.Fragment, null, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.progressBarHeight || "Progress & Volume Bar Height:"), /* @__PURE__ */ React.createElement(Stepper, { value: progressBarHeight, min: 1, max: 20, onChange: (v2) => {
      setProgressBarHeightState(v2);
      setProgressBarHeight(v2);
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.progressBarRadius || "Progress & Volume Bar Border Radius:"), /* @__PURE__ */ React.createElement(Stepper, { value: progressBarRadius, min: 0, max: 20, onChange: (v2) => {
      setProgressBarRadiusState(v2);
      setProgressBarRadius(v2);
    } }))), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.progressBarCompat || "Compatibility Mode:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.progressBarCompat })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRowControls" }, /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: progressBarCompat,
        onChange: (checked) => {
          setProgressBarCompatState(checked);
          setProgressBarCompat(checked, ensureProgressBarRadiusApplied);
        }
      }
    )))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.coverArt || "Cover Art" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.playbarCoverBorderRadius || "Cover Art Border Radius:"), /* @__PURE__ */ React.createElement(Stepper, { value: playbarCoverRadius, min: 0, max: 50, onChange: (v2) => {
      setPlaybarCoverRadius(v2);
      applyPlaybarCoverBorderRadius(v2);
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.comfyCoverArt?.enabled || "Comfy Cover Art:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.comfyCoverArt })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRowControls" }, /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: ccaEnabled === "show",
        onChange: (checked) => {
          const v2 = checked ? "show" : "hide";
          setCcaEnabled(v2);
          localStorage.setItem(CCA_ENABLED_KEY, v2);
          applyComfyCoverArt();
        }
      }
    ))), ccaEnabled === "show" && /* @__PURE__ */ React.createElement("div", { className: "liquifySubBlock" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.comfyCoverArt?.width || "Width (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: ccaWidth, min: 16, max: 200, onChange: (v2) => {
      setCcaWidth(v2);
      localStorage.setItem(CCA_WIDTH_KEY, String(v2));
      applyComfyCoverArt();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.comfyCoverArt?.height || "Height (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: ccaHeight, min: 16, max: 200, onChange: (v2) => {
      setCcaHeight(v2);
      localStorage.setItem(CCA_HEIGHT_KEY, String(v2));
      applyComfyCoverArt();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.comfyCoverArt?.marginBottom || "Margin Bottom (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: ccaMarginBottom, min: -50, max: 200, onChange: (v2) => {
      setCcaMarginBottom(v2);
      localStorage.setItem(CCA_MARGIN_BOTTOM_KEY, String(v2));
      applyComfyCoverArt();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.comfyCoverArt?.marginLeft || "Margin Left (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: ccaMarginLeft, min: -50, max: 200, onChange: (v2) => {
      setCcaMarginLeft(v2);
      localStorage.setItem(CCA_MARGIN_LEFT_KEY, String(v2));
      applyComfyCoverArt();
    } })))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.modes || "Modes" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.floatingPlayer || "Floating Player:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.floatingPlayer })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: floatingPlayer === "on",
        onChange: (checked) => {
          const v2 = checked ? "on" : "off";
          setFloatingPlayer(v2);
          applyFloatingPlayer(v2);
        }
      }
    )), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.transparentPlayer || "Transparent Player:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.transparentPlayer })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: transparentPlayer === "on",
        onChange: (checked) => {
          const v2 = checked ? "on" : "off";
          setTransparentPlayer(v2);
          applyTransparentPlayer(v2);
        }
      }
    )), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.compactPlayer || "Compact Player:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.compactPlayer })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: compactPlayer === "on",
        onChange: (checked) => {
          const v2 = checked ? "on" : "off";
          setCompactPlayer(v2);
          applyCompactPlayer(v2);
        }
      }
    )), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.playerControlIcons || "Use New Player Icons:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.playerControlIcons })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: playerIcons === "on",
        onChange: (checked) => {
          const v2 = checked ? "on" : "off";
          setPlayerIcons(v2);
          setPlayerControlIcons(v2);
        }
      }
    )), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.connectBar || "Show Connect Bar:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.connectBar })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: connectBar === "show",
        onChange: (checked) => {
          const v2 = checked ? "show" : "hide";
          setConnectBar(v2);
          applyConnectBar(v2);
        }
      }
    )))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-nextSongCard", title: titles.nextSongCard || "Next Song Card" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.show || "Show Next Song Card:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.nextSongCard })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRowControls" }, /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: nscShow === "show",
        onChange: (checked) => {
          const v2 = checked ? "show" : "hide";
          setNscShow(v2);
          localStorage.setItem(NSC_SHOW_KEY, v2);
          fireNscUpdate();
        }
      }
    ))), nscShow === "show" && /* @__PURE__ */ React.createElement("div", { className: "liquifySubBlock" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.position || "Horizontal Position"), /* @__PURE__ */ React.createElement("div", { className: "liquifyRowControls" }, /* @__PURE__ */ React.createElement(
      Select,
      {
        value: nscPosition,
        options: [
          { value: "left", label: t.nextSongCard?.left || "Left" },
          { value: "right", label: t.nextSongCard?.right || "Right" }
        ],
        onChange: (v2) => {
          setNscPosition(v2);
          localStorage.setItem(NSC_POSITION_KEY, v2);
          fireNscUpdate();
        }
      }
    ))), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.cardHeight || "Card Height (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscHeight, min: 32, max: 200, onChange: (v2) => {
      setNscHeight(v2);
      localStorage.setItem(NSC_HEIGHT_KEY, String(v2));
      fireNscUpdate();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.cardMaxWidth || "Card Max Width (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscMaxWidth, min: 100, max: 600, onChange: (v2) => {
      setNscMaxWidth(v2);
      localStorage.setItem(NSC_MAX_WIDTH_KEY, String(v2));
      fireNscUpdate();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.gap || "Gap between Image and Text (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscGap, min: 0, max: 24, onChange: (v2) => {
      setNscGap(v2);
      localStorage.setItem(NSC_GAP_KEY, String(v2));
      fireNscUpdate();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.coverSize || "Cover Size (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscCoverSize, min: 16, max: 128, onChange: (v2) => {
      setNscCoverSize(v2);
      localStorage.setItem(NSC_COVER_SIZE_KEY, String(v2));
      fireNscUpdate();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.hPad || "Horizontal Padding (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscHPad, min: 0, max: 32, onChange: (v2) => {
      setNscHPad(v2);
      localStorage.setItem(NSC_HPAD_KEY, String(v2));
      fireNscUpdate();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.vPad || "Vertical Padding (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscVPad, min: 0, max: 32, onChange: (v2) => {
      setNscVPad(v2);
      localStorage.setItem(NSC_VPAD_KEY, String(v2));
      fireNscUpdate();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.gapToPlayer || "Distance to Player (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscGapToPlayer, min: 0, max: 40, onChange: (v2) => {
      setNscGapToPlayer(v2);
      localStorage.setItem(NSC_GAP_PLAYER_KEY, String(v2));
      fireNscUpdate();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.borderRadius || "Border Radius (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscBorderRadius, min: 0, max: 50, onChange: (v2) => {
      setNscBorderRadius(v2);
      localStorage.setItem(NSC_BORDER_RADIUS_KEY, String(v2));
      fireNscUpdate();
    } })), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow", style: { margin: 0 } }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.nextSongCard?.coverBorderRadius || "Cover Border Radius (px)"), /* @__PURE__ */ React.createElement(Stepper, { value: nscCoverBorderRadius, min: 0, max: 50, onChange: (v2) => {
      setNscCoverBorderRadius(v2);
      localStorage.setItem(NSC_COVER_BORDER_RADIUS_KEY, String(v2));
      fireNscUpdate();
    } })))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-canvasCoverArt", title: titles.canvasCoverArt || "Canvas Cover Art" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.canvasCoverArt?.mode || "Track Name Cover Art:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.canvasCoverArt })), /* @__PURE__ */ React.createElement(
      Select,
      {
        value: npvcMode,
        options: [
          { value: "off", label: t.canvasCoverArt?.off || "Off" },
          { value: "trackInfo", label: t.canvasCoverArt?.trackInfo || "Next to Track Info" },
          { value: "outsideTrackInfo", label: t.canvasCoverArt?.outsideTrackInfo || "Outside Track Info" }
        ],
        onChange: (v2) => {
          setNpvcMode(v2);
          localStorage.setItem(NPVC_MODE_KEY, v2);
          window.dispatchEvent(new Event("liquifyNpvcUpdate"));
        }
      }
    )), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.canvasCoverArt?.showAlways || "Show Always:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.canvasShowAlways })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: npvcShowAlways === "yes",
        onChange: (checked) => {
          const v2 = checked ? "yes" : "no";
          setNpvcShowAlways(v2);
          localStorage.setItem(NPVC_SHOW_ALWAYS_KEY, v2);
          window.dispatchEvent(new Event("liquifyNpvcUpdate"));
        }
      }
    ))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-playlist", title: titles.playlist }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.playlistHeaderBox, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.playlistHeaderBox })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: playlistHeader === "show",
        onChange: (checked) => applyPlaylistHeaderMode(checked ? "show" : "hide")
      }
    )), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.actionBarBox || "Action Bar Box:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.actionBarBox })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: actionBarBox === "show",
        onChange: (checked) => applyActionBarBoxMode(checked ? "show" : "hide")
      }
    ))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-lyrics", title: titles.lyrics || "Lyrics" }, /* @__PURE__ */ React.createElement(SubSection, { title: sub.styling || "Styling" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.themedLyrics || "Themed Lyrics:", /* @__PURE__ */ React.createElement(HelpTip, { text: tips.themedLyrics })), /* @__PURE__ */ React.createElement(
      Toggle,
      {
        checked: themedLyrics === "on",
        onChange: (checked) => {
          setThemedLyricsState(checked ? "on" : "off");
          setThemedLyrics(checked);
        }
      }
    )), themedLyrics === "on" && /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.lyricsFontSize || "Lyrics Font Size:"), /* @__PURE__ */ React.createElement(Stepper, { value: lyricsFontSize, min: 10, max: 150, onChange: (v2) => {
      setLyricsFontSizeState(v2);
      setLyricsFontSize(v2);
    } })), themedLyrics === "on" && /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.lyricsMargin || "Lyrics Margin:"), /* @__PURE__ */ React.createElement(Stepper, { value: lyricsMargin, min: 0, max: 120, onChange: (v2) => {
      setLyricsMarginState(v2);
      setLyricsMargin(v2);
    } }))), /* @__PURE__ */ React.createElement(SubSection, { title: sub.translation || "Translation" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.lyricsMode || "Lyrics Translation/Romanization:"), /* @__PURE__ */ React.createElement("div", { className: "liquifyRowControls" }, /* @__PURE__ */ React.createElement(
      Select,
      {
        value: lyricsMode,
        options: [
          { value: "off", label: t.lyricsOptions?.off || "Off" },
          { value: "translation", label: t.lyricsOptions?.translation || "Translation only" },
          { value: "romanization", label: t.lyricsOptions?.romanization || "Romanization only" },
          { value: "both", label: t.lyricsOptions?.both || "Translation + Romanization" }
        ],
        onChange: applyLyricsMode
      }
    ))))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-transparent", title: titles.transparent }, /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.transparentWidth, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.transparentWidth })), /* @__PURE__ */ React.createElement("div", { style: { opacity: unixLike ? 0.5 : 1, pointerEvents: unixLike ? "none" : "auto" } }, /* @__PURE__ */ React.createElement(Stepper, { value: tcW, min: 0, max: 400, onChange: (v2) => applyTransparent(v2, tcH) }))), /* @__PURE__ */ React.createElement("div", { className: "liquifyRow" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyLabel" }, t.transparentHeight, /* @__PURE__ */ React.createElement(HelpTip, { text: tips.transparentHeight })), /* @__PURE__ */ React.createElement("div", { style: { opacity: unixLike ? 0.5 : 1, pointerEvents: unixLike ? "none" : "auto" } }, /* @__PURE__ */ React.createElement(Stepper, { value: tcH, min: 0, max: 300, onChange: (v2) => applyTransparent(tcW, v2) })))), /* @__PURE__ */ React.createElement(Section, { id: "liquify-sec-config", title: titles.config || "Config" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyConfigBlock" }, /* @__PURE__ */ React.createElement("div", { className: "liquifyConfigHint" }, cfg.hint || "Copy your current Liquify config, or paste one and apply it. Background images aren't included."), /* @__PURE__ */ React.createElement(
      "textarea",
      {
        className: "liquifyControlSurface liquifyConfigTextarea",
        spellCheck: false,
        value: configText,
        onChange: (e) => {
          setConfigText(e.target.value);
          setConfigDirty(true);
          setConfigStatus(null);
        }
      }
    ), configStatus && /* @__PURE__ */ React.createElement("div", { className: "liquifyConfigStatus" + (configStatus.ok ? " isOk" : " isError") }, configStatus.msg), /* @__PURE__ */ React.createElement("div", { className: "liquifyConfigActions" }, /* @__PURE__ */ React.createElement("button", { type: "button", className: "liquifyControlSurface liquifyActionBtn", onClick: handleConfigCopy }, cfg.copy || "Copy"), /* @__PURE__ */ React.createElement("button", { type: "button", className: "liquifyControlSurface liquifyActionBtn liquifyConfigApplyBtn", onClick: handleConfigApply }, cfg.apply || "Paste & Apply")))), /* @__PURE__ */ React.createElement("div", { style: { display: "flex", justifyContent: "center", marginTop: "16px", marginBottom: "8px" } }, /* @__PURE__ */ React.createElement(
      "button",
      {
        type: "button",
        className: "liquifyControlSurface liquifyActionBtn liquifyResetBtn",
        onClick: handleReset,
        style: { padding: "8px 24px", fontSize: "14px" }
      },
      t.resetAllSettings || "Reset all Settings"
    ))));
  }

  // src/settings/modal.tsx
  var OVERLAY_ID = "liquify-settings-react-overlay";
  var FLOATING_SETTINGS_SELECTOR = "body > .liquifyTooltipPopup, body > .liquifySectionNavScrollBtn";
  function removeFloatingSettingsElements() {
    document.querySelectorAll(FLOATING_SETTINGS_SELECTOR).forEach((el) => el.remove());
  }
  function hideFloatingSettingsElements() {
    document.querySelectorAll(FLOATING_SETTINGS_SELECTOR).forEach((el) => {
      el.style.display = "none";
    });
  }
  function createOverlay(onBackgroundClick) {
    const overlay = document.createElement("div");
    overlay.id = OVERLAY_ID;
    overlay.style.position = "fixed";
    overlay.style.inset = "0";
    overlay.style.zIndex = "99999";
    overlay.style.display = "flex";
    overlay.style.alignItems = "center";
    overlay.style.justifyContent = "center";
    overlay.style.background = "transparent";
    overlay.style.overflow = "hidden";
    overlay.style.padding = "24px";
    overlay.addEventListener("click", (e) => {
      if (e.target === overlay) onBackgroundClick(overlay);
    });
    return overlay;
  }
  function showOverlay(overlay) {
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        overlay.classList.add("overlay-visible");
      });
    });
  }
  function unmountOverlay(overlay, root) {
    try {
      const mountRoot = root || overlay.querySelector("div");
      if (mountRoot) ReactDOM.unmountComponentAtNode(mountRoot);
    } catch {
    }
    removeFloatingSettingsElements();
    overlay.remove();
  }
  function closeWithAnimation(overlay, root) {
    overlay.classList.remove("overlay-visible");
    overlay.classList.add("overlay-closing");
    hideFloatingSettingsElements();
    const panel = overlay.querySelector(".liquifySettingsPanel");
    let closed = false;
    let fallback = 0;
    const onEnd = (e) => {
      if (e && e.propertyName && e.propertyName !== "transform") return;
      if (closed) return;
      closed = true;
      window.clearTimeout(fallback);
      panel?.removeEventListener("transitionend", onEnd);
      unmountOverlay(overlay, root);
    };
    if (panel) panel.addEventListener("transitionend", onEnd);
    fallback = window.setTimeout(onEnd, 500);
  }
  function SettingsModalRoot(props) {
    const [nonce, setNonce] = React.useState(0);
    React.useEffect(() => {
      const handler = () => setNonce((n) => n + 1);
      window.addEventListener("liquifyConfigApplied", handler);
      window.addEventListener("liquifyLanguageChanged", handler);
      return () => {
        window.removeEventListener("liquifyConfigApplied", handler);
        window.removeEventListener("liquifyLanguageChanged", handler);
      };
    }, []);
    const SettingsContentAny = SettingsContent;
    return /* @__PURE__ */ React.createElement(SettingsContentAny, { key: nonce, onClose: props.onClose, artistCtrl: props.artistCtrl });
  }
  function openSettingsModal(artistCtrl) {
    ensureSettingsUiStyle();
    document.getElementById(OVERLAY_ID)?.remove();
    const overlay = createOverlay((target) => closeWithAnimation(target, root));
    const root = document.createElement("div");
    document.body.appendChild(overlay);
    overlay.appendChild(root);
    showOverlay(overlay);
    const onClose = () => closeWithAnimation(overlay, root);
    ReactDOM.render(/* @__PURE__ */ React.createElement(SettingsModalRoot, { onClose, artistCtrl }), root);
  }

  // src/settings/index.tsx
  function applySavedGlowSettings() {
    const mode = localStorage.getItem("liquify-glow-mode") || "default";
    const color = localStorage.getItem("liquify-glow-color") || "#1DB954";
    if (mode === "custom") applyGlowAccent(color);
    else resetGlowAccentToDefault();
  }
  function applySavedAccentSettings() {
    const mode = localStorage.getItem("liquify-accent-mode") || "dynamic";
    const color = localStorage.getItem("liquify-custom-color") || "#1DB954";
    if (!localStorage.getItem("liquify-accent-mode")) {
      localStorage.setItem("liquify-accent-mode", "dynamic");
    }
    if (mode === "custom") applyAccent2(color);
    else if (mode === "dynamic") applyDynamicAccent();
    else resetAccentToDefault();
  }
  function applySavedLayoutSettings() {
    ensureLibraryApplied();
    applySavedBackground();
    ensurePlayerApplied();
    ensureTransparentControlsApplied();
    ensureBackgroundAppearanceApplied();
    ensureArtistScrollEffectApplied();
    applySavedPlaylistHeader();
    applySavedActionBarBox();
    applySavedTransparentPlayer();
    applySavedFloatingPlayer();
    applySavedConnectBar();
    applySavedCompactPlayer();
    ensureProgressBarHeightApplied();
    ensureProgressBarRadiusApplied();
    ensureLayoutRadiusApplied();
    ensureThemedLyricsApplied();
    ensureGlassBlurApplied();
    ensureSidebarBlurApplied();
    ensureFontsApplied();
    ensureVinylApplied();
    ensureLocalFilesTransparentApplied();
    applySavedHomeLayout();
    applyComfyCoverArt(false);
    ensurePlaybarCoverBorderRadiusApplied();
    ensurePopupBounceApplied();
  }
  function applyAllSavedSettings(artistCtrl) {
    applySavedGlowSettings();
    applySavedAccentSettings();
    applySavedLayoutSettings();
    installPlayerControlIcons();
    setGlassEnabled(localStorage.getItem("liquify-glass-enabled") !== "off");
    try {
      artistCtrl?.setMode?.(localStorage.getItem("liquify-artist-bg-mode") || "theme");
    } catch {
    }
    for (const ev of [
      "liquifyNscUpdate",
      "liquifyNpvcUpdate",
      "liquifyLyricsModeChange",
      "liquifyAccentColorParamsChange",
      "liquifyBackgroundChange",
      "liquifyPlaybarCoverRadiusChange"
    ]) {
      window.dispatchEvent(new Event(ev));
    }
  }
  function pushDynamicAccent() {
    const mode = localStorage.getItem("liquify-accent-mode") || "dynamic";
    if (mode === "dynamic") applyDynamicAccent();
  }
  function installDynamicAccentObserver(anyWin) {
    if (anyWin.liquifyDynamicObserverTs) return;
    anyWin.liquifyDynamicObserverTs = new MutationObserver(pushDynamicAccent);
    anyWin.liquifyDynamicObserverTs.observe(document.body, { attributes: true, subtree: true });
    window.addEventListener("liquifyAccentColorReady", pushDynamicAccent);
  }
  function registerSettingsModal(artistCtrl) {
    window.showLiquifySettingsMenu = () => {
      try {
        openSettingsModal(artistCtrl);
      } catch (e) {
        console.error("Liquify settings open failed", e);
      }
    };
  }
  function installFeatureControllers() {
    installLyricsTranslator();
    installPlaylistIndicatorVisualizer();
    installHomeScreenVisualizer();
    installNextSongCard();
    installNowPlayingViewCover();
    installCoverSwipe();
    installPlayerControlIcons();
    installShareButtonTransition();
  }
  async function startLiquifySettings() {
    const anyWin = window;
    if (anyWin.liquifyStandaloneTsInitialized) return;
    anyWin.liquifyStandaloneTsInitialized = true;
    await awaitSpicetifyReact();
    applySavedGlowSettings();
    applySavedAccentSettings();
    applySavedLayoutSettings();
    installDynamicAccentObserver(anyWin);
    installFullscreenWatcher();
    const artistCtrl = installArtistBackgroundController();
    registerSettingsModal(artistCtrl);
    window.liquifyApplyAllSettings = () => applyAllSavedSettings(artistCtrl);
    initLiquifyGearInjection(getTranslation());
    startLiquifyOnboarding();
    reconcileLiquidLyricsInstall().catch(() => {
    });
    await awaitSpicetifyPlayer();
    installFeatureControllers();
  }

  // src/theme.ts
  var GLASS_TARGETS = [
    {
      // Top bar background layer (made transparent in user.css)
      selector: ".main-topBar-background",
      options: { borderRadius: 8 }
    },
    {
      // Dropdown / panel container (made transparent in user.css)
      selector: ".zddkQq3wlxEOg6aa",
      options: { borderRadius: 20, glassBlur: "5px" }
    },
    {
      selector: ".main-trackList-trackListHeader",
      options: { borderRadius: 20 }
    },
    {
      selector: ".Root__now-playing-bar",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-globalNav-searchInputContainer .main-topBar-searchBar",
      options: { borderRadius: 17 }
    },
    {
      selector: ".Root__globalNav .main-globalNav-navLink",
      options: { borderRadius: 17 }
    },
    {
      selector: ".NJh1B8rnlSUlK7sY",
      options: { borderRadius: 20, glassBlur: "5px" }
    },
    {
      selector: ".search-searchCategory-carouselButton",
      options: { borderRadius: 12 }
    },
    {
      selector: ".e-10451-box--tinted",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-entityHeader-container",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-actionBar-ActionBar",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-nowPlayingView-headerWrapper",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-nowPlayingView-section",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-nowPlayingView-trackInfo",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-contextMenu-tippy",
      options: { borderRadius: 10 }
    },
    {
      selector: ".PromotionButtonTooltip-module_tooltip-animation__cE-rt",
      options: { borderRadius: 12 }
    },
    {
      selector: ".iiX8td2tfVETS09_ button",
      options: { borderRadius: 13 }
    },
    {
      selector: ".gpBiAnJHb1gq46qV",
      options: { borderRadius: 20 }
    },
    {
      selector: ".view-homeShortcutsGrid-shortcut",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-home-filterChipsSection",
      options: { borderRadius: 20 }
    },
    {
      selector: "dialog",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-topBar-buddyFeed",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-userWidget-box",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-globalNav-historyButtons",
      options: { borderRadius: 20 }
    },
    {
      selector: ".EU1ylDKh7s2oMU0g",
      options: { borderRadius: 12 }
    },
    {
      selector: ".VwcQJ4Zsf0JKD3Ls",
      options: { borderRadius: 12 }
    },
    {
      selector: ".KQDsZX3kwwuAFpE8",
      options: { borderRadius: 20 }
    },
    {
      selector: ".KFAJvMWTSagxYXGC",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-trackList-trackListRow.q8suB2R_XkoUyIeZ",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-nowPlayingView-actionButton",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-playlistEditDetailsModal-container",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-playlistEditDetailsModal-imageDropDownButton",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-home-content section",
      options: { borderRadius: 20 }
    },
    {
      selector: ".os-scrollbar-handle",
      options: { borderRadius: 20 }
    },
    {
      selector: ".JDUQ8zTo6EUgHoYt",
      options: { borderRadius: 20 }
    },
    {
      selector: ".B9ji6YIpLSUHiyxx",
      options: { borderRadius: 20 }
    },
    {
      selector: ".gu0S9_98ZXIo5DaV",
      options: { borderRadius: 20 }
    },
    {
      selector: ".SUjhgyMvTou7TddO",
      options: { borderRadius: 20 }
    },
    {
      selector: ".ERRo1Br0ZQtJYVhz",
      options: { borderRadius: 20 }
    },
    {
      selector: ".LR7w41pC8ccVc11Q",
      options: { borderRadius: 20 }
    },
    {
      selector: ".VsYY0YB3c4lmhoDI",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-shelf-shelf",
      options: { borderRadius: 20 }
    },
    {
      selector: ".CO34wNPAbR8mpcdD",
      options: { borderRadius: 20 }
    },
    {
      selector: ".JthDv0xUCm8rLhu6",
      options: { borderRadius: 20 }
    },
    {
      selector: ".tlq9Tt69FX4bauLX",
      options: { borderRadius: 20 }
    },
    {
      selector: ".x-settings-section",
      options: { borderRadius: 20 }
    },
    {
      selector: ".fa_L1qIbh7QDDd_h",
      options: { borderRadius: 20 }
    },
    {
      selector: ".TguLwQ522LIEgpK_.IxVxBUbV5M5tGaEx",
      options: { borderRadius: 20 }
    },
    {
      selector: ".HOf9H18Ya0DkJ4_K",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-nowPlayingView-onTourItemGrid:hover",
      options: { borderRadius: 20 }
    },
    {
      selector: ".qm0mrbeno_z0mpoo",
      options: { borderRadius: 20 }
    },
    {
      selector: ".N3kf5S8O84aeaCZu",
      options: { borderRadius: 20 }
    },
    {
      selector: ".J8g7rZ2MDknxmiYP",
      options: { borderRadius: 20 }
    },
    {
      selector: ".Root__cinema-view",
      options: { borderRadius: 20, glassBlur: "10px" }
    },
    {
      selector: ".n5KI8mwa5o8qbn4b",
      options: { borderRadius: 20 }
    },
    {
      selector: ".vado7sbDrEsKhSmn",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-embedWidgetGenerator-container",
      options: { borderRadius: 20 }
    },
    {
      selector: ".e-10451-box--elevated",
      options: { borderRadius: 15 }
    },
    {
      selector: ".Wzl40f9FIUD91O2o",
      options: { borderRadius: 20 }
    },
    {
      selector: ".mcZzjuAJUvE9X4gX",
      options: { borderRadius: 20, glassBlur: "10px", applyTo: "before" }
    },
    {
      selector: ".Htdd9HRV28F07Dwl",
      options: { borderRadius: 20 }
    },
    {
      selector: ".p67WtOnm9lsRLOu2",
      options: { borderRadius: 20 }
    },
    {
      selector: ".JLkvt5ABTQYw_rG7",
      options: { borderRadius: 20 }
    },
    {
      selector: ".mKvcoJ_veYlcHwOz",
      options: { borderRadius: 20 }
    },
    {
      selector: ".tMcqYZ2om0nYbgrw",
      options: { borderRadius: 20 }
    },
    {
      selector: ".yZCluNwEsPD2zYyY",
      options: { borderRadius: 20 }
    },
    {
      selector: ".Hrce4GF4EEkPJdBI",
      options: { borderRadius: 20 }
    },
    {
      selector: ".EpfIE3glwAOGcNT6",
      options: { borderRadius: 20 }
    },
    {
      // Glass on a ::before layer behind the menu items (see user.css
      // .liquify-glass--before), so the items stay crisp above the refraction.
      selector: ".main-contextMenu-menu",
      options: { borderRadius: 20, glassBlur: "5px", applyTo: "before" }
    },
    {
      // Popup panel — glass on a ::before layer (see user.css .liquify-glass--before).
      selector: ".xamNkt5LX9o8aL1q",
      options: { borderRadius: 20, glassBlur: "5px", applyTo: "before" }
    },
    {
      selector: ".marketplace-header",
      options: { borderRadius: 20 }
    },
    {
      selector: ".marketplace-tabBar-active",
      options: { borderRadius: 20 }
    },
    {
      selector: ".Dropdown-menu",
      options: { borderRadius: 20 }
    },
    {
      selector: "#marketplace-readme",
      options: { borderRadius: 20 }
    },
    {
      selector: ".liquifySettingsPanel",
      options: { borderRadius: 20, glassBlur: "5px" }
    },
    {
      selector: ".liquifySelectMenu",
      options: { borderRadius: 15 }
    },
    {
      selector: ".liquifySectionNavScrollBtn",
      options: { borderRadius: 12 }
    },
    {
      // The toggle's knob is our glass lens (the colored liquid + goo morph come
      // from settingsStyles.tsx). Glass goes on the knob itself — not the whole
      // pill — so it doesn't nest behind the toggle's own backdrop. Small element:
      // a gentle distortion and no chromatic aberration keep the refraction clean
      // at ~26x24px. It only translates (and scales on press), so the displacement
      // map stays valid without regeneration.
      selector: ".liquid-toggle .indicator__liquid",
      options: { borderRadius: 999, distortionScale: -14, chromaticAberration: false }
    },
    {
      selector: "#liquify-next-song-card",
      options: { borderRadius: 20 }
    },
    {
      selector: ".main-card-card",
      options: { borderRadius: 20 }
    },
    {
      selector: "#liquify-settings-gear-btn",
      options: { borderRadius: 20 }
    },
    {
      selector: ".artist-artistDiscography-topBar.artist-artistDiscography-topBarScrolled",
      options: { borderRadius: 20 }
    },
    {
      selector: ".wJiY1vDfuci2a4db",
      options: { borderRadius: 20 }
    },
    {
      selector: ".oc3OomY6r9UoIEQ0",
      options: { borderRadius: 20 }
    },
    {
      selector: ".oReO3E1Df2odSFHX",
      options: { borderRadius: 10 }
    },
    {
      selector: ".fA6CNWFY1WQBCde9",
      options: { borderRadius: 10 }
    },
    {
      selector: ".main-trackCreditsModal-container",
      options: { borderRadius: 10 }
    },
    {
      selector: ".TGvpaalpJK0BKYYL",
      options: { borderRadius: 10, glassBlur: "5px" }
    },
    {
      selector: ".edvX5XPBIXITSQoH",
      options: { borderRadius: 10 }
    }
  ];
  var PRECISE_TARGETS = [
    { selector: ".main-trackList-trackListHeader", options: { borderRadius: 20 } },
    { selector: ".main-topBar-background", options: { borderRadius: 0 } },
    { selector: ".znOINyqAy7ivIGbQyrbt", options: { borderRadius: 20, glassBlur: "5px" } },
    { selector: ".iGRaSZDa1r0m21aF6oZq", options: { borderRadius: 20 } },
    { selector: ".niJOWstqVyfckHcXQxP1 .cSZJwcwYgJfwduUmXOOV", options: { borderRadius: 20 } },
    { selector: ".main-nowPlayingView-trackInfo", options: { borderRadius: 20 } },
    { selector: ".main-nowPlayingView-section", options: { borderRadius: 20 } },
    { selector: ".main-entityHeader-container.gmKBgPCnX785KDicbdJu", options: { borderRadius: 20 } },
    { selector: ".main-home-filterChipsSection", options: { borderRadius: 20 } },
    { selector: ".view-homeShortcutsGrid-shortcut", options: { borderRadius: 20 } },
    { selector: ".main-card-card", options: { borderRadius: 20 } },
    { selector: ".Root__globalNav .DoxYADBBjYMvoYwl7QPg", options: { borderRadius: 50 } },
    { selector: ".yfJeY2Xi99dPOe6fsIha", options: { borderRadius: 20 } },
    { selector: ".main-entityHeader-container.main-entityHeader-containerNormal", options: { borderRadius: 20 } },
    { selector: ".x-settings-section", options: { borderRadius: 20 } },
    { selector: ".LR7w41pC8ccVc11Q", options: { borderRadius: 20 } },
    { selector: ".ERRo1Br0ZQtJYVhz", options: { borderRadius: 20 } },
    { selector: ".HOf9H18Ya0DkJ4_K", options: { borderRadius: 20 } },
    { selector: ".main-entityHeader-imageContainerWrapper", options: { borderRadius: 20 } },
    { selector: ".JDUQ8zTo6EUgHoYt", options: { borderRadius: 20 } },
    { selector: ".main-globalNav-searchInputContainer .main-topBar-searchBar", options: { borderRadius: 17 } },
    { selector: ".Root__globalNav .main-globalNav-navLink", options: { borderRadius: 17 } },
    // Context menu / popup: glass on a ::before layer (see user.css .liquify-glass--before).
    { selector: ".main-contextMenu-menu", options: { borderRadius: 20, glassBlur: "5px", applyTo: "before" } },
    { selector: ".xamNkt5LX9o8aL1q", options: { borderRadius: 20, glassBlur: "5px", applyTo: "before" } },
    // Settings toggle knob — tiny, gentle distortion, no chromatic aberration.
    { selector: ".liquid-toggle .indicator__liquid", options: { borderRadius: 999, distortionScale: -14, chromaticAberration: false } }
  ];
  var preciseSelectors = new Set(PRECISE_TARGETS.map((t) => t.selector));
  var DARKENED = {
    ".liquifySettingsPanel": 0.8,
    ".liquifySelectMenu": 0.8
  };
  var BULK_TARGETS = GLASS_TARGETS.filter((t) => !preciseSelectors.has(t.selector)).map((t) => {
    const gb = t.options?.glassBlur;
    return {
      selector: t.selector,
      blur: gb ? parseInt(gb, 10) : void 0,
      before: t.options?.applyTo === "before" ? true : void 0,
      brightness: DARKENED[t.selector]
    };
  });
  function start() {
    installGlassDevtools();
    watchGlassTargets(PRECISE_TARGETS);
    applyBulkGlass(BULK_TARGETS);
    startBackground();
    startPopupBounce();
    startLiquifySettings();
  }
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", start, { once: true });
  } else {
    start();
  }
})();
