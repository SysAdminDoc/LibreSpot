# LibreSpot live customization engine

This component owns the live state inside Spotify. It changes palette variables, layer classes, effects tiers, reviewed snippets, and client-side feature overrides without running `spicetify apply`.

The engine keeps one managed style element for the active palette and another for enabled snippets. Layout, effects, and accessibility are independent classes on the document root. Removing the managed styles and classes returns the page to its prior state.

## Workspace

- `src/core` contains the runtime, `color.ini` parser, profile and theme export, signal adapters, and named health checks.
- `tests` uses Vitest with happy-dom. Files run one at a time to stay within the Spotify fixture memory limit.
- `vendor` holds pinned upstream source parts. See [third-party notices](THIRD_PARTY_NOTICES.md).

## Check it

```powershell
pnpm install --frozen-lockfile
pnpm run check
```

`pnpm run check` runs ESLint, the strict TypeScript build, and the DOM test suite.

The component is [AGPL-3.0](LICENSE). The rest of LibreSpot keeps its root license.
