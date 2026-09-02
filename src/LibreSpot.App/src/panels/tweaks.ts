import type { ArrangementItem, EngineState } from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import { SURFACE_SNIPPETS } from "../surface/builtins.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import {
  PanelIntro,
  Section,
  SpotifyIcon,
  ToggleRow,
  h,
} from "../surface/ui.ts";

function update(
  properties: PanelProperties,
  mutator: (draft: EngineState) => void,
  notice: string,
): void {
  void properties.runtime.update(mutator, notice);
}

const COVER_ART_SNIPPETS = new Set([
  "rounded-cover-art",
  "circular-cover-art",
  "square-cover-art",
]);

const SNIPPET_PREVIEW_ICONS: Readonly<Record<string, string>> = {
  "hidden-control": "x",
  "hidden-panel": "artist",
  "compact-rows": "list-view",
  "quiet-scrollbar": "menu",
  "centered-lines": "lyrics",
  "progress-bar": "play",
  "quiet-panel": "album",
  "window-controls": "fullscreen",
};

function snippetPreview(id: string, preview: string): UiNode {
  const image = document.querySelector<HTMLImageElement>(
    '.Root__now-playing-bar img[src^="https://"], .Root__nav-bar img[src^="https://"]',
  );
  if ((COVER_ART_SNIPPETS.has(id) || preview === "sidebar-art") && image) {
    return h(
      "div",
      {
        className: `librespot-snippet-preview is-${preview}`,
        "aria-hidden": "true",
      },
      h("img", { src: image.currentSrc || image.src, alt: "", loading: "lazy" }),
    );
  }
  return h(
    "div",
    {
      className: `librespot-snippet-preview is-${preview}`,
      "aria-hidden": "true",
    },
    SpotifyIcon({ name: SNIPPET_PREVIEW_ICONS[preview] ?? "album" }),
  );
}

export function updateSnippetSelection(
  current: readonly string[],
  id: string,
  checked: boolean,
): string[] {
  const selected = new Set(current);
  if (checked && COVER_ART_SNIPPETS.has(id)) {
    for (const coverArtId of COVER_ART_SNIPPETS) {
      selected.delete(coverArtId);
    }
  }
  if (checked) {
    selected.add(id);
  } else {
    selected.delete(id);
  }
  return [...selected];
}

function normalized(value: string): string {
  return value.replace(/\s+/g, " ").trim().toLocaleLowerCase();
}

function mergedArrangementItems(
  savedOrder: readonly string[],
  available: readonly ArrangementItem[],
): ArrangementItem[] {
  const remaining = new Map(available.map((item) => [item.id, item]));
  const ordered: ArrangementItem[] = [];
  for (const saved of savedOrder) {
    const match =
      remaining.get(saved) ??
      [...remaining.values()].find(
        (item) => normalized(item.label) === normalized(saved),
      );
    if (match) {
      ordered.push(match);
      remaining.delete(match.id);
    } else {
      ordered.push({ id: saved, label: saved });
    }
  }
  ordered.push(...remaining.values());
  return ordered;
}

function arrangement(
  properties: PanelProperties,
  title: string,
  key: "homeSections" | "sidebarItems",
  available: readonly ArrangementItem[],
  emptyMessage: string,
): UiNode {
  const items = mergedArrangementItems(
    properties.snapshot.state[key],
    available,
  );
  const move = (index: number, direction: -1 | 1) => {
    const destination = index + direction;
    if (destination < 0 || destination >= items.length) {
      return;
    }
    const next = items.map((item) => item.id);
    const current = next[index];
    const target = next[destination];
    if (!current || !target) {
      return;
    }
    next[index] = target;
    next[destination] = current;
    update(
      properties,
      (draft) => {
        draft[key] = next;
      },
      `${title} order updated`,
    );
  };

  return h(
    "div",
    { className: "librespot-arrangement" },
    h("h3", null, title),
    h(
      "ol",
      null,
      ...items.map((item, index) =>
        h(
          "li",
          { key: item },
          h("span", null, item.label),
          h(
            "span",
            { className: "librespot-arrangement__actions" },
            h(
              "button",
              {
                type: "button",
                "aria-label": `Move ${item.label} up`,
                disabled: index === 0,
                onClick: () => {
                  move(index, -1);
                },
              },
              "Up",
            ),
            h(
              "button",
              {
                type: "button",
                "aria-label": `Move ${item.label} down`,
                disabled: index === items.length - 1,
                onClick: () => {
                  move(index, 1);
                },
              },
              "Down",
            ),
          ),
        ),
      ),
      items.length === 0
        ? h("p", { className: "librespot-empty-state" }, emptyMessage)
        : null,
    ),
  );
}

export function TweaksPanel(properties: PanelProperties): UiNode {
  const enabled = new Set(properties.snapshot.state.enabledSnippets);
  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "tweaks" },
    PanelIntro({
      eyebrow: "Reviewed CSS",
      title: "Tweaks",
      body: "Small changes stay separate from themes. Each rule has a source and a verified Spotify version, and toggles live through one managed style element.",
    }),
    Section({
      title: "Snippet catalog",
      description: `${SURFACE_SNIPPETS.length} reviewed rules use stable anchors and apply through the live style layer.`,
      children: h(
        "div",
        { className: "librespot-catalog-list" },
        ...SURFACE_SNIPPETS.map((snippet) =>
          h(
            "article",
            { className: "librespot-catalog-card", key: snippet.id },
            snippetPreview(snippet.id, snippet.preview),
            ToggleRow({
              label: snippet.title,
              description: snippet.description,
              checked: enabled.has(snippet.id),
              badge: snippet.category,
              onChange: (checked) => {
                update(
                  properties,
                  (draft) => {
                    draft.enabledSnippets = updateSnippetSelection(
                      draft.enabledSnippets,
                      snippet.id,
                      checked,
                    );
                  },
                  `${snippet.title} ${checked ? "enabled" : "disabled"}`,
                );
              },
            }),
            h(
              "div",
              { className: "librespot-catalog-meta" },
              h("span", null, `Spotify ${snippet.lastVerifiedSpotify}`),
              h("span", null, snippet.sourceTitle),
              h(
                "a",
                {
                  href: snippet.source,
                  target: "_blank",
                  rel: "noreferrer",
                },
                "Source",
              ),
            ),
          ),
        ),
      ),
    }),
    Section({
      title: "Page arrangement",
      description: "The engine reads the running client, stores order by stable identity, and reapplies it whenever Spotify redraws either surface.",
      children: h(
        "div",
        { className: "librespot-arrangement-grid" },
        arrangement(
          properties,
          "Home sections",
          "homeSections",
          properties.snapshot.availableHomeSections,
          "Open Home once to capture its current sections.",
        ),
        arrangement(
          properties,
          "Library items",
          "sidebarItems",
          properties.snapshot.availableSidebarItems,
          "Open Your Library once to capture its visible items.",
        ),
      ),
    }),
  );
}
