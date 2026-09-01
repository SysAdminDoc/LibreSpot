import type { EngineState } from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import { SURFACE_SNIPPETS } from "../surface/builtins.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import { PanelIntro, Section, ToggleRow, h } from "../surface/ui.ts";

function update(
  properties: PanelProperties,
  mutator: (draft: EngineState) => void,
  notice: string,
): void {
  void properties.runtime.update(mutator, notice);
}

function arrangement(
  properties: PanelProperties,
  title: string,
  key: "homeSections" | "sidebarItems",
  fallback: readonly string[],
): UiNode {
  const items =
    properties.snapshot.state[key].length > 0
      ? properties.snapshot.state[key]
      : [...fallback];
  const move = (index: number, direction: -1 | 1) => {
    const destination = index + direction;
    if (destination < 0 || destination >= items.length) {
      return;
    }
    const next = [...items];
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
          h("span", null, item),
          h(
            "span",
            { className: "librespot-arrangement__actions" },
            h(
              "button",
              {
                type: "button",
                "aria-label": `Move ${item} up`,
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
                "aria-label": `Move ${item} down`,
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
      description: "The catalog expands from Marketplace data in the next layer. These reviewed rules exercise the live path.",
      children: h(
        "div",
        { className: "librespot-catalog-list" },
        ...SURFACE_SNIPPETS.map((snippet) =>
          h(
            "article",
            { className: "librespot-catalog-card", key: snippet.id },
            ToggleRow({
              label: snippet.title,
              description: snippet.description,
              checked: enabled.has(snippet.id),
              badge: snippet.category,
              onChange: (checked) => {
                update(
                  properties,
                  (draft) => {
                    const selected = new Set(draft.enabledSnippets);
                    if (checked) {
                      selected.add(snippet.id);
                    } else {
                      selected.delete(snippet.id);
                    }
                    draft.enabledSnippets = [...selected];
                  },
                  `${snippet.title} ${checked ? "enabled" : "disabled"}`,
                );
              },
            }),
            h(
              "div",
              { className: "librespot-catalog-meta" },
              h("span", null, `Spotify ${snippet.lastVerifiedSpotify}`),
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
      description: "The engine stores order by stable identity and reapplies it after navigation under the global navigation layout.",
      children: h(
        "div",
        { className: "librespot-arrangement-grid" },
        arrangement(properties, "Home sections", "homeSections", [
          "Made for you",
          "Recently played",
          "Your mixes",
          "Popular albums",
        ]),
        arrangement(properties, "Sidebar items", "sidebarItems", [
          "Home",
          "Search",
          "Your Library",
          "Liked Songs",
        ]),
      ),
    }),
  );
}
