import type { UiNode } from "../spicetify-globals.d.ts";
import { SURFACE_PRESETS } from "../surface/builtins.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import {
  ActionButton,
  PanelIntro,
  Section,
  h,
} from "../surface/ui.ts";

export function PresetsPanel(properties: PanelProperties): UiNode {
  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "presets" },
    PanelIntro({
      eyebrow: "Whole-profile changes",
      title: "Presets",
      body: "A preset fills theme, scheme, layers, flags, snippets, and scale together. Every field stays editable after apply.",
      action: ActionButton({
        label: "Copy current profile",
        secondary: true,
        onClick: () => {
          void properties.runtime.copyProfile();
        },
      }),
    }),
    Section({
      title: "Built-in presets",
      description: "These four starting points use the same state model as hand-tuned profiles.",
      children: h(
        "div",
        { className: "librespot-preset-grid" },
        ...SURFACE_PRESETS.map((preset) =>
          h(
            "article",
            {
              className:
                properties.snapshot.state.name === preset.title
                  ? "librespot-preset-card is-active"
                  : "librespot-preset-card",
              key: preset.id,
            },
            h(
              "div",
              { className: "librespot-preset-card__preview", "aria-hidden": "true" },
              h("span", { className: `preset-preview preset-preview--${preset.id}` }),
            ),
            h("h3", null, preset.title),
            h("p", null, preset.description),
            ActionButton({
              label:
                properties.snapshot.state.name === preset.title
                  ? "Applied"
                  : "Apply preset",
              disabled: properties.snapshot.state.name === preset.title,
              onClick: () => {
                void properties.runtime.update(
                  (draft) => {
                    preset.apply(draft);
                  },
                  `${preset.title} preset applied`,
                );
              },
            }),
          ),
        ),
      ),
    }),
    Section({
      title: "Your current profile",
      description: "Copy it as a .librespot payload, then import it in the desktop app or share it as text.",
      children: h(
        "div",
        { className: "librespot-profile-summary" },
        h(
          "div",
          null,
          h("span", { className: "librespot-eyebrow" }, "Profile"),
          h("strong", null, properties.snapshot.state.name),
        ),
        h(
          "dl",
          null,
          h("div", null, h("dt", null, "Theme"), h("dd", null, properties.snapshot.state.theme)),
          h("div", null, h("dt", null, "Scheme"), h("dd", null, properties.snapshot.activeScheme)),
          h("div", null, h("dt", null, "Effects"), h("dd", null, properties.snapshot.state.effectsTier)),
          h(
            "div",
            null,
            h("dt", null, "Snippets"),
            h("dd", null, String(properties.snapshot.state.enabledSnippets.length)),
          ),
        ),
      ),
    }),
  );
}
