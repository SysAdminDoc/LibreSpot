import arrowRightIcon from "lucide-static/icons/arrow-right.svg";
import searchIcon from "lucide-static/icons/search.svg";
import compactPreview from "../assets/theme-previews/compact.png";
import prismPreview from "../assets/theme-previews/prism.png";
import type { ArrangementItem, EngineState } from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import { SURFACE_SNIPPETS } from "../surface/builtins.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import {
  PanelIntro,
  Section,
  ToggleRow,
  eventTarget,
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

function lucideIcon(source: string, className: string): UiNode {
  return h("span", {
    className,
    "aria-hidden": "true",
    dangerouslySetInnerHTML: { __html: source },
  });
}

function snippetPreview(
  preview: string,
  state: "before" | "after" | "catalog" = "catalog",
): UiNode {
  const source = preview === "compact-rows" ? compactPreview : prismPreview;
  return h(
    "div",
    {
      className: `librespot-snippet-preview is-${preview} is-${state}`,
      "aria-hidden": "true",
    },
    h("img", {
      src: source,
      alt: "",
      loading: "lazy",
      decoding: "async",
      draggable: false,
    }),
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
          { key: item.id },
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
  const React = Spicetify.React;
  const enabled = new Set(properties.snapshot.state.enabledSnippets);
  const categories = ["All", ...new Set(SURFACE_SNIPPETS.map((snippet) => snippet.category))];
  const [query, setQuery] = React.useState("");
  const [category, setCategory] = React.useState("All");
  const [selectedId, setSelectedId] = React.useState(
    properties.snapshot.state.enabledSnippets[0] ?? SURFACE_SNIPPETS[0]?.id ?? "",
  );
  const normalizedQuery = query.trim().toLocaleLowerCase();
  const filtered = SURFACE_SNIPPETS.filter((snippet) =>
    (category === "All" || snippet.category === category) &&
    (!normalizedQuery || [snippet.title, snippet.description, snippet.category, snippet.sourceTitle]
      .some((value) => value.toLocaleLowerCase().includes(normalizedQuery))),
  );
  const selectedSnippet =
    filtered.find((snippet) => snippet.id === selectedId) ?? filtered[0] ?? null;

  const toggleSnippet = (id: string, title: string, checked: boolean): void => {
    update(
      properties,
      (draft) => {
        draft.enabledSnippets = updateSnippetSelection(
          draft.enabledSnippets,
          id,
          checked,
        );
      },
      `${title} ${checked ? "enabled" : "disabled"}`,
    );
  };

  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "tweaks" },
    PanelIntro({
      eyebrow: "Reviewed CSS",
      title: "Tweaks",
      body: "Small changes stay separate from themes. Each rule has a source and a verified Spotify version, and toggles live through one managed style element.",
    }),
    h(
      "div",
      { className: "librespot-tweak-toolbar" },
      h(
        "label",
        { className: "librespot-tweak-search" },
        lucideIcon(searchIcon, "librespot-tweak-search__icon"),
        h("span", { className: "librespot-visually-hidden" }, "Search tweaks"),
        h("input", {
          type: "search",
          value: query,
          placeholder: "Search tweaks",
          "aria-label": "Search tweaks",
          onInput: (event: unknown) => {
            const target = eventTarget(event);
            if (target instanceof HTMLInputElement) setQuery(target.value);
          },
        }),
      ),
      h(
        "div",
        { className: "librespot-tweak-filters", role: "group", "aria-label": "Tweak categories" },
        ...categories.map((item) => h(
          "button",
          {
            type: "button",
            key: item,
            className: category === item ? "is-active" : "",
            "aria-pressed": String(category === item),
            onClick: () => setCategory(item),
          },
          item,
        )),
      ),
    ),
    selectedSnippet
      ? h(
          "section",
          { className: "librespot-tweak-spotlight", "aria-label": `${selectedSnippet.title} preview` },
          h(
            "div",
            { className: "librespot-tweak-spotlight__copy" },
            h(
              "span",
              { className: enabled.has(selectedSnippet.id) ? "librespot-live-state is-on" : "librespot-live-state" },
              enabled.has(selectedSnippet.id) ? "Live now" : "Ready to preview",
            ),
            h("h2", null, selectedSnippet.title),
            h("p", null, selectedSnippet.description),
            ToggleRow({
              label: enabled.has(selectedSnippet.id) ? "Enabled" : "Disabled",
              description: "This change applies to Spotify immediately.",
              checked: enabled.has(selectedSnippet.id),
              onChange: (checked) => toggleSnippet(selectedSnippet.id, selectedSnippet.title, checked),
            }),
          ),
          h(
            "div",
            { className: "librespot-tweak-spotlight__comparison" },
            h(
              "div",
              { className: "librespot-tweak-spotlight__frame" },
              h("span", null, "Before"),
              snippetPreview(selectedSnippet.preview, "before"),
            ),
            lucideIcon(arrowRightIcon, "librespot-tweak-spotlight__arrow"),
            h(
              "div",
              { className: "librespot-tweak-spotlight__frame is-after" },
              h("span", null, "After"),
              snippetPreview(selectedSnippet.preview, "after"),
            ),
            h(
              "a",
              {
                href: selectedSnippet.source,
                target: "_blank",
                rel: "noreferrer",
              },
              `Source · Spotify ${selectedSnippet.lastVerifiedSpotify}`,
            ),
          ),
        )
      : null,
    Section({
      title: "Snippet catalog",
      description: `${filtered.length} of ${SURFACE_SNIPPETS.length} reviewed rules shown.`,
      className: "librespot-section--catalog",
      children: h(
        "div",
        { className: "librespot-catalog-list" },
        ...filtered.map((snippet) =>
          h(
            "article",
            {
              className: snippet.id === selectedSnippet?.id
                ? "librespot-catalog-card is-selected"
                : "librespot-catalog-card",
              key: snippet.id,
            },
            h(
              "button",
              {
                type: "button",
                className: "librespot-catalog-card__preview",
                "aria-label": `Preview ${snippet.title}`,
                onClick: () => setSelectedId(snippet.id),
              },
              snippetPreview(snippet.preview),
              h("span", null, "Preview"),
            ),
            ToggleRow({
              label: snippet.title,
              description: snippet.description,
              checked: enabled.has(snippet.id),
              onChange: (checked) => toggleSnippet(snippet.id, snippet.title, checked),
            }),
            h(
              "div",
              { className: "librespot-catalog-meta" },
              h("span", null, `Spotify ${snippet.lastVerifiedSpotify}`),
              h("span", null, snippet.category),
              h(
                "a",
                {
                  href: snippet.source,
                  target: "_blank",
                  rel: "noreferrer",
                  title: snippet.sourceTitle,
                },
                "Source",
              ),
            ),
          ),
        ),
        filtered.length === 0
          ? h(
              "div",
              { className: "librespot-empty-state", role: "status" },
              h("strong", null, "No matching tweaks"),
              h("p", null, "Try another name or category."),
              h(
                "button",
                {
                  type: "button",
                  className: "librespot-button librespot-button--secondary",
                  onClick: () => {
                    setQuery("");
                    setCategory("All");
                  },
                },
                "Clear filters",
              ),
            )
          : null,
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
