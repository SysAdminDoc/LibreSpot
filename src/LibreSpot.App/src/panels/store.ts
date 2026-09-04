import themePreviewDocument from "../../../../schemas/theme-preview-manifest.json";
import appWindowIcon from "lucide-static/icons/app-window.svg";
import puzzleIcon from "lucide-static/icons/puzzle.svg";
import searchIcon from "lucide-static/icons/search.svg";
import shieldCheckIcon from "lucide-static/icons/shield-check.svg";
import accessibilityPreview from "../assets/theme-previews/accessibility.png";
import compactPreview from "../assets/theme-previews/compact.png";
import prismPreview from "../assets/theme-previews/prism.png";
import {
  CUSTOMIZATION_CATALOG,
  type CatalogAsset,
  type CatalogSpicetifyOption,
  type CatalogTheme,
  type EngineState,
  type FeatureValue,
} from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import {
  ActionButton,
  InputRow,
  PanelIntro,
  ToggleRow,
  eventTarget,
  h,
} from "../surface/ui.ts";
import { displaySchemeName } from "../surface/labels.ts";
import {
  APP_DESCRIPTIONS,
  EXTENSION_DESCRIPTIONS,
  THEME_DESCRIPTIONS,
} from "./store-metadata.ts";

type StoreTab = "themes" | "extensions" | "apps";
const STORE_TABS: readonly StoreTab[] = ["themes", "extensions", "apps"];
type ThemePreview = {
  url: string | null;
  status: "available" | "unavailable" | "broken" | "placeholder";
  fallbackLabel: string;
};
type PreviewManifestTheme = {
  id: string;
  source: "official" | "community" | "virtual" | "bundled";
  sourceRepo: string | null;
  schemes: string[];
  requiresJs: boolean;
  marketplaceOnly: boolean;
  supportState: string;
  preview: ThemePreview;
};
type StoreTheme = CatalogTheme & {
  sourceKind: PreviewManifestTheme["source"];
  preview?: ThemePreview;
  builtIn: boolean;
};

const previewManifestThemes = themePreviewDocument.themes as PreviewManifestTheme[];
const previewById = new Map(previewManifestThemes.map((theme) => [theme.id, theme]));
const builtInIds = new Set(CUSTOMIZATION_CATALOG.builtInThemes.map((theme) => theme.id));
const builtInPreviews: Readonly<Record<string, string>> = {
  Accessibility: accessibilityPreview,
  Compact: compactPreview,
  Prism: prismPreview,
};

export const STORE_THEMES: readonly StoreTheme[] = (() => {
  const themes = new Map<string, StoreTheme>();
  for (const theme of CUSTOMIZATION_CATALOG.builtInThemes) {
    const manifest = previewById.get(theme.id);
    const previewUrl = builtInPreviews[theme.id];
    themes.set(theme.id, {
      ...theme,
      ...(manifest?.requiresJs === undefined ? {} : { requiresJs: manifest.requiresJs }),
      ...(manifest?.sourceRepo ? { source: manifest.sourceRepo } : {}),
      ...(manifest?.supportState ? { supportState: manifest.supportState } : {}),
      sourceKind: manifest?.source ?? "bundled",
      ...(previewUrl
        ? {
            preview: {
              url: previewUrl,
              status: "available" as const,
              fallbackLabel: `${theme.title} theme preview`,
            },
          }
        : manifest?.preview ? { preview: manifest.preview } : {}),
      builtIn: true,
    });
  }
  for (const theme of CUSTOMIZATION_CATALOG.themes) {
    const manifest = previewById.get(theme.id);
    if (theme.marketplaceOnly || manifest?.marketplaceOnly || builtInIds.has(theme.id)) continue;
    themes.set(theme.id, {
      ...theme,
      sourceKind: manifest?.source ?? "official",
      ...(manifest?.preview ? { preview: manifest.preview } : {}),
      ...(manifest?.supportState ? { supportState: manifest.supportState } : {}),
      builtIn: false,
    });
  }
  return [...themes.values()];
})();

export function countInstalledManagedAssets(
  installed: readonly string[],
  managedAssets: readonly CatalogAsset[],
): number {
  const installedIds = new Set(installed);
  return managedAssets.reduce(
    (count, asset) => count + (installedIds.has(asset.id) ? 1 : 0),
    0,
  );
}

function titleForTheme(theme: StoreTheme): string {
  return theme.id === "text" ? "Text" : theme.title;
}

export function themeDescription(theme: StoreTheme): string {
  return theme.description ?? THEME_DESCRIPTIONS[theme.id] ??
    `A reviewed Spotify theme with ${theme.schemes.length} color ${theme.schemes.length === 1 ? "scheme" : "schemes"}.`;
}

function sourceLabel(theme: StoreTheme): string {
  if (theme.sourceKind === "bundled") return "Made by LibreSpot";
  if (theme.sourceKind === "community") return "Community theme";
  return "Spicetify Themes";
}

function SvgIcon(properties: { source: string; className?: string }): UiNode {
  return h("span", {
    className: properties.className,
    "aria-hidden": "true",
    dangerouslySetInnerHTML: { __html: properties.source },
  });
}

function ThemeArtwork(properties: {
  theme: StoreTheme;
  large?: boolean;
  eager?: boolean;
}): UiNode {
  const theme = properties.theme;
  const suppliedPreviewUrl = theme.preview?.status === "available" ? theme.preview.url : null;
  const previewUrl = suppliedPreviewUrl ?? prismPreview;
  return h(
    "div",
    {
      className: [
        "librespot-theme-artwork",
        properties.large ? "is-large" : "",
        theme.builtIn ? `is-${theme.id.toLowerCase()}` : "",
        suppliedPreviewUrl ? "" : "has-fallback",
      ].filter(Boolean).join(" "),
      "data-theme-name": titleForTheme(theme),
    },
    h("img", {
      src: previewUrl,
      alt: suppliedPreviewUrl
        ? `${titleForTheme(theme)} theme preview in Spotify`
        : `${titleForTheme(theme)} preview unavailable. LibreSpot Prism shown instead.`,
      loading: properties.eager ? "eager" : "lazy",
      decoding: "async",
      referrerPolicy: "no-referrer",
      onError: (event: unknown) => {
        const target = eventTarget(event);
        if (!(target instanceof HTMLImageElement) || target.dataset.fallback === "true") return;
        target.dataset.fallback = "true";
        target.src = prismPreview;
        target.alt = `${titleForTheme(theme)} preview unavailable. LibreSpot Prism shown instead.`;
        target.parentElement?.classList.add("has-fallback");
      },
    }),
    h("span", { className: "librespot-theme-artwork__error" }, "Preview unavailable"),
    theme.builtIn
      ? h("span", { className: "librespot-theme-artwork__live" }, "Live preview")
      : null,
  );
}

function ThemeCard(properties: {
  theme: StoreTheme;
  selected: boolean;
  installed: boolean;
  onSelect: () => void;
}): UiNode {
  const theme = properties.theme;
  return h(
    "button",
    {
      type: "button",
      className: properties.selected
        ? "librespot-theme-card is-selected"
        : "librespot-theme-card",
      "aria-pressed": String(properties.selected),
      "aria-label": `Preview ${titleForTheme(theme)} theme`,
      onClick: properties.onSelect,
    },
    h(ThemeArtwork, { theme }),
    h(
      "span",
      { className: "librespot-theme-card__body" },
      h(
        "span",
        { className: "librespot-theme-card__title" },
        h("strong", null, titleForTheme(theme)),
        properties.installed
          ? h("span", { className: "librespot-health-dot is-healthy", "aria-label": "Installed" })
          : theme.supportState && theme.supportState !== "active"
            ? h("span", { className: "librespot-health-dot is-warning", "aria-label": "Compatibility notice" })
          : null,
      ),
      h("span", { className: "librespot-theme-card__description" }, themeDescription(theme)),
      h(
        "span",
        { className: "librespot-theme-card__meta" },
        h("span", null, sourceLabel(theme)),
        h("span", null, `${theme.schemes.length} ${theme.schemes.length === 1 ? "scheme" : "schemes"}`),
      ),
    ),
  );
}

function itemDescription(asset: CatalogAsset, kind: "extension" | "app"): string {
  const description =
    (kind === "extension" ? EXTENSION_DESCRIPTIONS[asset.id] : APP_DESCRIPTIONS[asset.id]) ??
    asset.description ??
    "A reviewed addition managed by LibreSpot Desktop.";
  const trimmed = description.trim();
  return /[.!?]$/.test(trimmed) ? trimmed : `${trimmed}.`;
}

function licenseLabel(asset: CatalogAsset): string {
  if (asset.license === "NOASSERTION") return "License not declared";
  return asset.license ?? "Bundled";
}

function AssetIcon(properties: { kind: "extension" | "app" }): UiNode {
  return SvgIcon({ source: properties.kind === "extension" ? puzzleIcon : appWindowIcon });
}

function AssetCard(properties: {
  asset: CatalogAsset;
  kind: "extension" | "app";
  installed: boolean;
  included: boolean;
  openDesktop: () => void;
}): UiNode {
  const asset = properties.asset;
  const kindLabel = properties.kind === "extension" ? "Extension" : "App";
  return h(
    "article",
    { className: "librespot-store-asset-card", key: `${properties.kind}:${asset.id}` },
    h(
      "div",
      { className: "librespot-store-asset-card__header" },
      h("span", { className: `librespot-store-asset-card__icon is-${properties.kind}` }, h(AssetIcon, { kind: properties.kind })),
      h(
        "span",
        { className: "librespot-store-asset-card__heading" },
        h("strong", null, asset.title),
        h("span", null, properties.included ? "Included with LibreSpot" : `${kindLabel}${properties.installed ? " installed" : ""}`),
      ),
      properties.installed || properties.included
        ? h("span", { className: "librespot-health-dot is-healthy", "aria-label": "Installed" })
        : null,
    ),
    h("p", null, itemDescription(asset, properties.kind)),
    h(
      "div",
      { className: "librespot-store-asset-card__facts" },
      h("span", null, asset.supportState === "active" || !asset.supportState ? "Reviewed" : asset.supportState),
      h("span", null, licenseLabel(asset)),
      h("span", null, `Spotify ${asset.lastVerifiedSpotify}`),
    ),
    h(
      "div",
      { className: "librespot-store-asset-card__footer" },
      h("span", { title: asset.source }, asset.source),
      ActionButton({
        label: properties.included ? "Included" : properties.installed ? "Review setup" : "Set up",
        accessibleLabel: properties.included ? `${asset.title} is included` : `Set up ${asset.title} in LibreSpot Desktop`,
        disabled: properties.included,
        secondary: properties.installed,
        onClick: properties.openDesktop,
      }),
    ),
  );
}

function optionValue(properties: PanelProperties, option: CatalogSpicetifyOption): FeatureValue {
  const saved = properties.snapshot.state.spicetifyOptions[option.id];
  if (saved !== undefined) return saved;
  if (option.id === "current_theme") return Spicetify.Config?.current_theme ?? "";
  if (option.id === "color_scheme") return properties.snapshot.state.scheme;
  if (option.id === "extensions") return properties.snapshot.installedExtensions.join("|");
  if (option.id === "custom_apps") return properties.snapshot.installedCustomApps.join("|");
  if (typeof option.default === "boolean" || typeof option.default === "number" || typeof option.default === "string") {
    return option.default;
  }
  return JSON.stringify(option.default);
}

function saveOption(properties: PanelProperties, option: CatalogSpicetifyOption, value: FeatureValue): void {
  void properties.runtime.update(
    (draft) => {
      draft.spicetifyOptions[option.id] = value;
      if (option.id === "color_scheme" && typeof value === "string" && draft.schemes[value]) draft.scheme = value;
      if (option.id === "replace_colors" && typeof value === "boolean") draft.layers.palette = value;
    },
    `${option.label} saved`,
  );
}

function optionControl(properties: PanelProperties, option: CatalogSpicetifyOption): UiNode {
  const value = optionValue(properties, option);
  if (option.type === "boolean") {
    return ToggleRow({
      label: option.label,
      description: option.description,
      checked: Boolean(value),
      badge: option.live ? "Engine bridge" : "Desktop apply",
      onChange: (checked) => saveOption(properties, option, checked),
    });
  }
  return InputRow({
    label: option.label,
    description: `${option.description} ${option.live ? "The matching engine control changes live." : "LibreSpot Desktop applies this value."}`,
    value: typeof value === "boolean" ? String(value) : value,
    type: option.type === "number" ? "number" : "text",
    onChange: (next) => saveOption(properties, option, option.type === "number" ? Number(next) : next),
  });
}

function matchesSearch(values: readonly (string | undefined)[], query: string): boolean {
  const normalized = query.trim().toLocaleLowerCase();
  return !normalized || values.some((value) => value?.toLocaleLowerCase().includes(normalized));
}

export function StorePanel(properties: PanelProperties): UiNode {
  const React = Spicetify.React;
  const initialTheme = STORE_THEMES.find((theme) => theme.id === properties.snapshot.state.theme) ?? STORE_THEMES[0];
  const [tab, setTab] = React.useState<StoreTab>("themes");
  const [search, setSearch] = React.useState("");
  const [selectedThemeId, setSelectedThemeId] = React.useState(initialTheme?.id ?? "Prism");
  const selectedTheme = STORE_THEMES.find((theme) => theme.id === selectedThemeId) ?? STORE_THEMES[0];
  const [selectedScheme, setSelectedScheme] = React.useState(selectedTheme?.schemes[0] ?? "Dark");
  const [previewingLive, setPreviewingLive] = React.useState(false);
  const installedTheme = Spicetify.Config?.current_theme ?? "";
  const installedExtensions = new Set(properties.snapshot.installedExtensions);
  const installedApps = new Set(properties.snapshot.installedCustomApps);

  React.useEffect(() => () => {
    properties.runtime.clearPreview();
  }, [properties.runtime]);

  const filteredThemes = React.useMemo(
    () => STORE_THEMES.filter((theme) => matchesSearch([
      titleForTheme(theme),
      themeDescription(theme),
      theme.source,
      sourceLabel(theme),
      ...theme.schemes,
    ], search)),
    [search],
  );
  const filteredExtensions = React.useMemo(
    () => CUSTOMIZATION_CATALOG.extensions.filter((asset) => matchesSearch([
      asset.title,
      asset.id,
      asset.source,
      itemDescription(asset, "extension"),
    ], search)),
    [search],
  );
  const filteredApps = React.useMemo(
    () => CUSTOMIZATION_CATALOG.customApps.filter((asset) => matchesSearch([
      asset.title,
      asset.id,
      asset.source,
      itemDescription(asset, "app"),
    ], search)),
    [search],
  );

  const selectTheme = (theme: StoreTheme): void => {
    if (previewingLive) properties.runtime.clearPreview();
    setPreviewingLive(false);
    setSelectedThemeId(theme.id);
    const currentScheme = theme.id === installedTheme ? Spicetify.Config?.color_scheme : undefined;
    setSelectedScheme(currentScheme && theme.schemes.includes(currentScheme) ? currentScheme : theme.schemes[0] ?? "Default");
  };

  const toggleLivePreview = (): void => {
    if (!selectedTheme?.builtIn) return;
    if (previewingLive) {
      properties.runtime.clearPreview();
      setPreviewingLive(false);
      return;
    }
    properties.runtime.previewTheme(selectedTheme.id, selectedScheme);
    setPreviewingLive(true);
  };

  const useBuiltInTheme = (): void => {
    if (!selectedTheme) return;
    properties.runtime.clearPreview();
    setPreviewingLive(false);
    void properties.runtime.update(
      (draft: EngineState) => {
        draft.theme = selectedTheme.id;
        if (draft.schemes[selectedScheme]) draft.scheme = selectedScheme;
      },
      `${titleForTheme(selectedTheme)} applied`,
    );
  };

  const tabCounts: Record<StoreTab, number> = {
    themes: STORE_THEMES.length,
    extensions: CUSTOMIZATION_CATALOG.extensions.length,
    apps: CUSTOMIZATION_CATALOG.customApps.length,
  };
  const visibleCount = tab === "themes" ? filteredThemes.length : tab === "extensions" ? filteredExtensions.length : filteredApps.length;

  const activateTab = (nextTab: StoreTab): void => {
    setTab(nextTab);
    setSearch("");
  };

  const moveTabFocus = (event: unknown, currentTab: StoreTab): void => {
    const keyboardEvent = event as { key?: string; preventDefault?: () => void };
    if (!keyboardEvent.key || !["ArrowLeft", "ArrowRight", "Home", "End"].includes(keyboardEvent.key)) return;
    keyboardEvent.preventDefault?.();
    const currentIndex = STORE_TABS.indexOf(currentTab);
    const nextIndex = keyboardEvent.key === "Home"
      ? 0
      : keyboardEvent.key === "End"
        ? STORE_TABS.length - 1
        : (currentIndex + (keyboardEvent.key === "ArrowRight" ? 1 : -1) + STORE_TABS.length) % STORE_TABS.length;
    const nextTab = STORE_TABS[nextIndex];
    if (!nextTab) return;
    document.getElementById(`librespot-store-tab-${nextTab}`)?.focus();
    activateTab(nextTab);
  };

  const spotlight = selectedTheme ? h(
    "article",
    { className: "librespot-store-spotlight" },
    h("div", { className: "librespot-store-spotlight__media" }, h(ThemeArtwork, { theme: selectedTheme, large: true, eager: true })),
    h(
      "div",
      { className: "librespot-store-spotlight__body" },
      h("span", { className: "librespot-eyebrow" }, sourceLabel(selectedTheme)),
      h("h2", null, titleForTheme(selectedTheme)),
      h("p", null, themeDescription(selectedTheme)),
      h(
        "div",
        { className: "librespot-store-spotlight__badges" },
        h("span", { className: "librespot-badge" }, selectedTheme.builtIn ? "Live" : "Desktop install"),
        selectedTheme.requiresJs ? h("span", { className: "librespot-badge" }, "Theme script") : null,
        selectedTheme.supportState && selectedTheme.supportState !== "active"
          ? h("span", { className: "librespot-badge librespot-badge--warning" }, "Compatibility notice")
          : null,
        h("span", { className: "librespot-badge" }, `Spotify ${selectedTheme.lastVerifiedSpotify ?? CUSTOMIZATION_CATALOG.pins.spotifyVersion}`),
      ),
      h(
        "label",
        { className: "librespot-store-scheme" },
        h("span", null, "Color scheme"),
        h(
          "select",
          {
            className: "librespot-select",
            value: selectedScheme,
            onChange: (event: unknown) => {
              const target = eventTarget(event);
              if (!(target instanceof HTMLSelectElement)) return;
              setSelectedScheme(target.value);
              if (previewingLive) properties.runtime.previewTheme(selectedTheme.id, target.value);
            },
          },
          ...selectedTheme.schemes.map((scheme) => h("option", { value: scheme, key: scheme }, displaySchemeName(scheme))),
        ),
      ),
      h(
        "div",
        { className: "librespot-inline-actions" },
        selectedTheme.builtIn
          ? ActionButton({
              label: previewingLive ? "End live preview" : "Preview live",
              accessibleLabel: `${previewingLive ? "End" : "Start"} ${titleForTheme(selectedTheme)} live preview`,
              secondary: true,
              onClick: toggleLivePreview,
            })
          : null,
        selectedTheme.builtIn
          ? ActionButton({
              label: properties.snapshot.state.theme === selectedTheme.id && properties.snapshot.state.scheme === selectedScheme ? "In use" : "Use now",
              disabled: properties.snapshot.state.theme === selectedTheme.id && properties.snapshot.state.scheme === selectedScheme,
              onClick: useBuiltInTheme,
            })
          : ActionButton({
              label: selectedTheme.id === installedTheme && selectedScheme === Spicetify.Config?.color_scheme ? "Review setup" : "Set up in Desktop",
              onClick: () => properties.runtime.openDesktopStore("theme", selectedTheme.id, selectedScheme),
            }),
      ),
    ),
  ) : null;

  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "store" },
    PanelIntro({
      eyebrow: "Curated for Spotify",
      title: "Store",
      body: "Discover every theme, extension, and app LibreSpot supports. Preview the look here, then apply live engine themes now or hand bundle changes to LibreSpot Desktop.",
      action: h(
        "div",
        { className: "librespot-store-summary", "aria-label": "Store catalog summary" },
        h("strong", null, String(STORE_THEMES.length)),
        h("span", null, "themes"),
        h("span", { className: "librespot-store-summary__divider", "aria-hidden": "true" }),
        h("strong", null, String(CUSTOMIZATION_CATALOG.extensions.length + CUSTOMIZATION_CATALOG.customApps.length)),
        h("span", null, "add-ons"),
      ),
    }),
    h(
      "div",
      { className: "librespot-store-trust" },
      SvgIcon({ source: shieldCheckIcon, className: "librespot-store-trust__icon" }),
      h(
        "div",
        null,
        h("strong", null, "One reviewed catalog"),
        h("span", null, "LibreSpot pins the source for every downloadable item and verifies it before Desktop installs it."),
      ),
    ),
    h(
      "div",
      { className: "librespot-store-toolbar" },
      h(
        "div",
        { className: "librespot-store-tabs", role: "tablist", "aria-label": "Store categories" },
        ...STORE_TABS.map((item) => h(
          "button",
          {
            type: "button",
            role: "tab",
            key: item,
            id: `librespot-store-tab-${item}`,
            "aria-controls": `librespot-store-panel-${item}`,
            "aria-selected": String(tab === item),
            tabIndex: tab === item ? 0 : -1,
            className: tab === item ? "is-active" : "",
            onClick: () => activateTab(item),
            onKeyDown: (event: unknown) => moveTabFocus(event, item),
          },
          `${item.charAt(0).toUpperCase()}${item.slice(1)}`,
          h("span", null, String(tabCounts[item])),
        )),
      ),
      h(
        "label",
        { className: "librespot-store-search" },
        SvgIcon({ source: searchIcon, className: "librespot-store-search__icon" }),
        h("span", { className: "librespot-store-search__label" }, "Search store"),
        h("input", {
          type: "search",
          value: search,
          placeholder: `Search ${tab}`,
          "aria-label": `Search ${tab}`,
          onChange: (event: unknown) => {
            const target = eventTarget(event);
            if (target instanceof HTMLInputElement) setSearch(target.value);
          },
        }),
      ),
    ),
    h(
      "div",
      {
        className: "librespot-store-tabpanel",
        role: "tabpanel",
        id: `librespot-store-panel-${tab}`,
        "aria-labelledby": `librespot-store-tab-${tab}`,
        tabIndex: 0,
      },
      tab === "themes" ? spotlight : null,
      visibleCount === 0
      ? h(
          "div",
          { className: "librespot-empty-state" },
          h("strong", null, `No ${tab} match “${search}”`),
          h("p", null, "Try an item name, feature, color scheme, or source."),
          ActionButton({ label: "Clear search", secondary: true, onClick: () => setSearch("") }),
        )
      : null,
      tab === "themes" && visibleCount > 0
      ? h(
          "section",
          { className: "librespot-store-results", "aria-label": "Theme catalog" },
          h(
            "div",
            { className: "librespot-store-results__heading" },
            h("h2", null, "All themes"),
            h("span", null, `${visibleCount} shown`),
          ),
          h(
            "div",
            { className: "librespot-theme-grid" },
            ...filteredThemes.map((theme) => h(ThemeCard, {
              theme,
              selected: theme.id === selectedTheme?.id,
              installed: theme.id === installedTheme || (theme.builtIn && theme.id === properties.snapshot.state.theme),
              onSelect: () => selectTheme(theme),
            })),
          ),
        )
      : null,
      tab === "extensions" && visibleCount > 0
      ? h(
          "section",
          { className: "librespot-store-results", "aria-label": "Extension catalog" },
          h(
            "div",
            { className: "librespot-store-results__heading" },
            h("h2", null, "Extensions"),
            h("span", null, `${countInstalledManagedAssets(properties.snapshot.installedExtensions, CUSTOMIZATION_CATALOG.extensions)} installed`),
          ),
          h(
            "div",
            { className: "librespot-store-asset-grid" },
            ...filteredExtensions.map((asset) => h(AssetCard, {
              asset,
              kind: "extension",
              installed: installedExtensions.has(asset.id),
              included: asset.id === "librespot-engine.js",
              openDesktop: () => properties.runtime.openDesktopStore("extension", asset.id),
            })),
          ),
        )
      : null,
      tab === "apps" && visibleCount > 0
      ? h(
          "section",
          { className: "librespot-store-results", "aria-label": "App catalog" },
          h(
            "div",
            { className: "librespot-store-results__heading" },
            h("h2", null, "Apps"),
            h("span", null, `${countInstalledManagedAssets(properties.snapshot.installedCustomApps, CUSTOMIZATION_CATALOG.customApps)} installed`),
          ),
          h(
            "div",
            { className: "librespot-store-asset-grid" },
            ...filteredApps.map((asset) => h(AssetCard, {
              asset,
              kind: "app",
              installed: installedApps.has(asset.id),
              included: asset.id === "librespot",
              openDesktop: () => properties.runtime.openDesktopStore("app", asset.id),
            })),
          ),
        )
        : null,
    ),
    h(
      "details",
      { className: "librespot-section librespot-disclosure-section librespot-store-advanced" },
      h(
        "summary",
        null,
        h(
          "span",
          { className: "librespot-disclosure-summary" },
          h(
            "span",
            { className: "librespot-disclosure-summary__copy" },
            h("strong", null, "Advanced Spicetify settings"),
            h("span", null, "Direct configuration values for experienced users and troubleshooting."),
          ),
          h("span", { className: "librespot-badge" }, `${CUSTOMIZATION_CATALOG.spicetifyOptions.length} options`),
        ),
      ),
      h(
        "div",
        { className: "librespot-section__body" },
        h(
          Spicetify.React.Fragment,
          null,
          ...CUSTOMIZATION_CATALOG.spicetifyOptions.map((option) =>
            h("div", { className: "librespot-feature", key: option.id }, optionControl(properties, option)),
          ),
        ),
      ),
    ),
  );
}
