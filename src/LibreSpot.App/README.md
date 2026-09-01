# LibreSpot live customization engine

This component owns the live state inside Spotify. It changes palette variables, layer classes, effects tiers, reviewed snippets, and client-side feature overrides without running `spicetify apply`.

The engine keeps one managed style element for the active palette and another for enabled snippets. Layout, effects, and accessibility are independent classes on the document root. Removing the managed styles and classes returns the page to its prior state.

## Workspace

- `src/core` contains the runtime, `color.ini` parser, profile and theme export, signal adapters, and named health checks.
- `../../schemas/librespot-customization.json` is the shared catalog. It records 348 flags from the pinned Spotify `xpui.js`, 31 SpotX controls, 21 Spicetify options, reviewed CSS, themes, extensions, custom apps, and source pins.
- `src/app.ts` is the Spotify custom app. Its rail opens Look, Tweaks, Features, Extensions, Presets, and Health.
- `src/extensions/librespot-engine.ts` keeps the engine loaded on every Spotify route and supplies a menu entry when custom-app navigation is unavailable.
- `src/panels` contains the six native React surfaces. The controls update the live runtime or clearly identify settings that need a desktop apply.
- `tests` uses Vitest with happy-dom. Files run one at a time to stay within the Spotify fixture memory limit.
- `vendor` holds pinned upstream source parts. See [third-party notices](THIRD_PARTY_NOTICES.md).

## Check it

```powershell
pnpm install --frozen-lockfile
pnpm run check
```

`pnpm run check` runs ESLint, checks the catalog against the pinned local Spotify bundle and SpotX source, builds the strict TypeScript project, checks the production bundle, and runs the DOM test suite. The bundle command creates `dist/index.js`, `dist/style.css`, `dist/manifest.json`, and `dist/librespot-engine.js` for Spicetify.

Use `pnpm run catalog:refresh` only when advancing the reviewed Spotify or SpotX pins. It reads the local pinned `xpui.js` and SpotX `patches.json`, then rewrites the shared catalog. `pnpm run catalog:truth` is read-only and fails if those sources drift.

The component is [AGPL-3.0](LICENSE). The rest of LibreSpot keeps its root license.
