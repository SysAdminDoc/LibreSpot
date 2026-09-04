import bookmarkIcon from "lucide-static/icons/bookmark.svg";
import accessibilityPreview from "../assets/theme-previews/accessibility.png";
import compactPreview from "../assets/theme-previews/compact.png";
import prismPreview from "../assets/theme-previews/prism.png";
import type { UiNode } from "../spicetify-globals.d.ts";
import { applyUserPreset, captureUserPreset } from "../core/index.ts";
import { SURFACE_PRESETS } from "../surface/builtins.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import { displaySchemeName } from "../surface/labels.ts";
import {
  ActionButton,
  PanelIntro,
  Section,
  eventTarget,
  h,
} from "../surface/ui.ts";

const PRESET_FACTS: Readonly<Record<string, readonly [string, string][]>> = {
  oled: [
    ["Scheme", "OLED"],
    ["Effects", "Glass, subtle"],
    ["Layers", "Minimal chrome"],
  ],
  accessibility: [
    ["Scheme", "High contrast"],
    ["Layout", "Larger scale"],
    ["Focus", "More visible"],
  ],
  compact: [
    ["Layout", "Compact"],
    ["Scale", "Smaller"],
    ["Navigation", "Thinner rail"],
  ],
  performance: [
    ["Effects", "Minimal"],
    ["Motion", "Reduced"],
    ["Rendering", "Optimized"],
  ],
};

function presetPreview(id: string): UiNode {
  const previews: Readonly<Record<string, string>> = {
    accessibility: accessibilityPreview,
    compact: compactPreview,
    oled: prismPreview,
    performance: prismPreview,
  };
  return h(
    "div",
    {
      className: `librespot-preset-card__preview is-${id}`,
      "aria-hidden": "true",
    },
    h("img", {
      src: previews[id] ?? prismPreview,
      alt: "",
      loading: "lazy",
      decoding: "async",
      draggable: false,
    }),
  );
}

function bookmarkGlyph(): UiNode {
  return h("span", {
    className: "librespot-preset-empty__icon",
    "aria-hidden": "true",
    dangerouslySetInnerHTML: { __html: bookmarkIcon },
  });
}

export function PresetsPanel(properties: PanelProperties): UiNode {
  const React = Spicetify.React;
  const [presetName, setPresetName] = React.useState("");
  const savePreset = () => {
    const name = presetName.trim() || `Preset ${properties.snapshot.state.userPresets.length + 1}`;
    void properties.runtime.update(
      (draft) => {
        const id = `user-${Date.now().toString(36)}`;
        draft.userPresets.push(captureUserPreset(draft, id, name));
      },
      `${name} saved`,
    );
    setPresetName("");
  };
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
      description: "Choose a starting point, review the outcome, then fine-tune anything.",
      className: "librespot-section--presets",
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
            presetPreview(preset.id),
            h("h3", null, preset.title),
            h("p", null, preset.description),
            h("span", { className: "librespot-preset-card__changes-label" }, "Changes"),
            h(
              "dl",
              { className: "librespot-preset-card__facts" },
              ...(PRESET_FACTS[preset.id] ?? []).map(([label, value]) => h(
                "div",
                { key: label },
                h("dt", null, label),
                h("dd", null, value),
              )),
            ),
            ActionButton({
              label:
                properties.snapshot.state.name === preset.title
                  ? "Applied"
                  : "Apply",
              accessibleLabel:
                properties.snapshot.state.name === preset.title
                  ? `${preset.title} preset applied`
                  : `Apply ${preset.title} preset`,
              secondary: true,
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
      title: "Saved presets",
      description: "Save the current controls into this .librespot profile, then apply or remove the preset here.",
      className: "librespot-section--saved-presets",
      children: h(
        Spicetify.React.Fragment,
        null,
        h(
          "div",
          { className: "librespot-preset-save" },
          h(
            "label",
            null,
            h("span", null, "Preset name"),
            h("input", {
              className: "librespot-input",
              type: "text",
              "aria-label": "Preset name",
              value: presetName,
              placeholder: "My preset",
              onInput: (event: unknown) => {
                const target = eventTarget(event);
                if (target instanceof HTMLInputElement) {
                  setPresetName(target.value);
                }
              },
            }),
          ),
          ActionButton({ label: "Save current", onClick: savePreset }),
        ),
        properties.snapshot.state.userPresets.length === 0
          ? h(
              "div",
              { className: "librespot-preset-empty", role: "status" },
              bookmarkGlyph(),
              h("strong", null, "No saved presets yet"),
              h("span", null, "Save the current profile to create your first one."),
            )
          : h(
              "div",
              { className: "librespot-extension-grid" },
              ...properties.snapshot.state.userPresets.map((preset) =>
                h(
                  "article",
                  { className: "librespot-extension-card", key: preset.id },
                  h("h3", null, preset.name),
                  h("p", null, `${preset.theme} / ${displaySchemeName(preset.scheme)} / ${preset.effectsTier}`),
                  h(
                    "div",
                    { className: "librespot-inline-actions" },
                    ActionButton({
                      label: "Apply",
                      onClick: () => {
                        void properties.runtime.update(
                          (draft) => {
                            applyUserPreset(draft, preset);
                          },
                          `${preset.name} applied`,
                        );
                      },
                    }),
                    ActionButton({
                      label: "Remove",
                      secondary: true,
                      onClick: () => {
                        void properties.runtime.update(
                          (draft) => {
                            draft.userPresets = draft.userPresets.filter((item) => item.id !== preset.id);
                          },
                          `${preset.name} removed`,
                        );
                      },
                    }),
                  ),
                ),
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
          h("div", null, h("dt", null, "Scheme"), h("dd", null, displaySchemeName(properties.snapshot.activeScheme))),
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
