export const LAYER_STYLE_ID = "librespot-engine-layers";
export const PALETTE_STYLE_ID = "librespot-engine-palette";
export const SNIPPET_STYLE_ID = "librespot-engine-snippets";

export const LAYER_CSS = `
:root {
  --librespot-accent: var(--spice-accent, var(--spice-button));
  --librespot-radius: 12px;
  --librespot-font: SpotifyMixUI, CircularSp, sans-serif;
  --librespot-scale-navigation: 1;
  --librespot-scale-content: 1;
  --librespot-scale-playbar: 1;
  --librespot-scale-right-sidebar: 1;
  --librespot-glass-alpha: 0.58;
  --librespot-glass-blur: 18px;
  --librespot-focus-width: 2px;
}

html.librespot-layer-layout body {
  font-family: var(--librespot-font);
}

html.librespot-layer-layout .Root__nav-bar,
html.librespot-layer-layout [data-testid="global-nav-bar"] {
  font-size: calc(1rem * var(--librespot-scale-navigation));
}

html.librespot-layer-layout .Root__main-view,
html.librespot-layer-layout [data-testid="main-view-container"] {
  font-size: calc(1rem * var(--librespot-scale-content));
}

html.librespot-layer-layout .Root__now-playing-bar,
html.librespot-layer-layout [data-testid="now-playing-bar"] {
  font-size: calc(1rem * var(--librespot-scale-playbar));
}

html.librespot-layer-layout .Root__right-sidebar,
html.librespot-layer-layout [data-testid="right-sidebar"] {
  font-size: calc(1rem * var(--librespot-scale-right-sidebar));
}

html.librespot-layer-layout .main-card-card,
html.librespot-layer-layout [data-testid="card"],
html.librespot-layer-layout .main-entityHeader-imageContainer img {
  border-radius: var(--librespot-radius);
}

html.librespot-layer-effects.librespot-tier-glass .Root__nav-bar,
html.librespot-layer-effects.librespot-tier-glass .Root__right-sidebar,
html.librespot-layer-effects.librespot-tier-glass .Root__now-playing-bar,
html.librespot-layer-effects.librespot-tier-glass [data-testid="global-nav-bar"],
html.librespot-layer-effects.librespot-tier-glass [data-testid="right-sidebar"],
html.librespot-layer-effects.librespot-tier-glass [data-testid="now-playing-bar"] {
  background-color: rgba(var(--spice-rgb-player), var(--librespot-glass-alpha));
  backdrop-filter: blur(var(--librespot-glass-blur)) saturate(1.18);
  -webkit-backdrop-filter: blur(var(--librespot-glass-blur)) saturate(1.18);
}

html.librespot-layer-effects.librespot-tier-eco {
  --librespot-glass-alpha: 0.9;
  --librespot-glass-blur: 0;
}

html.librespot-layer-effects.librespot-tier-eco .Root__nav-bar,
html.librespot-layer-effects.librespot-tier-eco .Root__right-sidebar,
html.librespot-layer-effects.librespot-tier-eco .Root__now-playing-bar,
html.librespot-layer-effects.librespot-tier-eco [data-testid="global-nav-bar"],
html.librespot-layer-effects.librespot-tier-eco [data-testid="right-sidebar"],
html.librespot-layer-effects.librespot-tier-eco [data-testid="now-playing-bar"] {
  background-color: rgba(var(--spice-rgb-player), var(--librespot-glass-alpha));
  backdrop-filter: none;
  -webkit-backdrop-filter: none;
}

html.librespot-tier-flat *,
html.librespot-reduced-motion * {
  backdrop-filter: none !important;
  -webkit-backdrop-filter: none !important;
  animation-duration: 0.001ms !important;
  animation-iteration-count: 1 !important;
  scroll-behavior: auto !important;
  transition-duration: 0.001ms !important;
}

html.librespot-layer-accessibility *:focus-visible {
  outline: var(--librespot-focus-width) solid var(--librespot-accent) !important;
  outline-offset: 3px !important;
}

html.librespot-layer-accessibility.librespot-high-contrast {
  --librespot-focus-width: 4px;
}

@media (prefers-reduced-motion: reduce) {
  * {
    backdrop-filter: none !important;
    -webkit-backdrop-filter: none !important;
    animation-duration: 0.001ms !important;
    animation-iteration-count: 1 !important;
    scroll-behavior: auto !important;
    transition-duration: 0.001ms !important;
  }
}
`;
