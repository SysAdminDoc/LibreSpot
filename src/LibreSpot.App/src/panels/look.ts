import type { EngineState } from "../core/index.ts";
import type { UiNode } from "../spicetify-globals.d.ts";
import type { PanelProperties } from "../surface/panel-types.ts";
import {
  PanelIntro,
  Section,
  SegmentedControl,
  SelectRow,
  SliderRow,
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

function eventValue(event: unknown): string {
  if (!(event instanceof Event)) {
    return "";
  }
  return event.target instanceof HTMLInputElement ? event.target.value : "";
}

function schemeCards(properties: PanelProperties): UiNode {
  const state = properties.snapshot.state;
  return h(
    "div",
    { className: "librespot-scheme-grid" },
    ...Object.entries(state.schemes).map(([name, scheme]) =>
      h(
        "button",
        {
          type: "button",
          key: name,
          className:
            name === properties.snapshot.activeScheme
              ? "librespot-scheme is-active"
              : "librespot-scheme",
          "aria-pressed": String(name === properties.snapshot.activeScheme),
          onMouseEnter: () => {
            properties.runtime.previewScheme(name);
          },
          onMouseLeave: () => {
            properties.runtime.clearPreview();
          },
          onFocus: () => {
            properties.runtime.previewScheme(name);
          },
          onBlur: () => {
            properties.runtime.clearPreview();
          },
          onClick: () => {
            update(
              properties,
              (draft) => {
                draft.scheme = name;
              },
              `${name} scheme applied`,
            );
          },
        },
        h("span", {
          className: "librespot-scheme__swatch",
          style: {
            "--scheme-main": `#${scheme.main ?? "121212"}`,
            "--scheme-accent": `#${scheme.accent ?? scheme.button ?? "1ED760"}`,
            "--scheme-text": `#${scheme.text ?? "FFFFFF"}`,
          },
        }),
        h("span", { className: "librespot-scheme__name" }, name),
        h(
          "span",
          { className: "librespot-scheme__hint" },
          name === state.scheme ? "Saved" : "Preview on focus",
        ),
      ),
    ),
  );
}

function scheduleControls(properties: PanelProperties): UiNode {
  const schedule = properties.snapshot.state.schedule;
  const timeControl = (
    label: string,
    value: string,
    onChange: (value: string) => void,
  ) =>
    h(
      "label",
      { className: "librespot-time-control" },
      h("span", null, label),
      h("input", {
        type: "time",
        value,
        onChange: (event: unknown) => {
          onChange(eventValue(event));
        },
      }),
    );

  return h(
    Spicetify.React.Fragment,
    null,
    ToggleRow({
      label: "Automatic light and dark",
      description: "Switch schemes from the local clock without relying on Spotify's forced dark mode.",
      checked: schedule.enabled,
      onChange: (checked) => {
        update(
          properties,
          (draft) => {
            draft.schedule.enabled = checked;
          },
          checked ? "Schedule enabled" : "Schedule disabled",
        );
      },
    }),
    h(
      "div",
      { className: "librespot-time-grid" },
      timeControl("Light starts", schedule.lightStart, (value) => {
        update(
          properties,
          (draft) => {
            draft.schedule.lightStart = value;
          },
          "Light schedule updated",
        );
      }),
      timeControl("Dark starts", schedule.darkStart, (value) => {
        update(
          properties,
          (draft) => {
            draft.schedule.darkStart = value;
          },
          "Dark schedule updated",
        );
      }),
    ),
    SelectRow({
      label: "Day scheme",
      description: "The scheme used between the two clock values.",
      value: schedule.lightScheme,
      options: Object.keys(properties.snapshot.state.schemes).map((name) => ({
        value: name,
        label: name,
      })),
      onChange: (value) => {
        update(
          properties,
          (draft) => {
            draft.schedule.lightScheme = value;
          },
          "Day scheme updated",
        );
      },
    }),
    SelectRow({
      label: "Night scheme",
      description: "The scheme used outside the daytime window.",
      value: schedule.darkScheme,
      options: Object.keys(properties.snapshot.state.schemes).map((name) => ({
        value: name,
        label: name,
      })),
      onChange: (value) => {
        update(
          properties,
          (draft) => {
            draft.schedule.darkScheme = value;
          },
          "Night scheme updated",
        );
      },
    }),
  );
}

export function LookPanel(properties: PanelProperties): UiNode {
  const state = properties.snapshot.state;
  const layerLabels: {
    key: keyof EngineState["layers"];
    label: string;
    description: string;
  }[] = [
    {
      key: "palette",
      label: "Palette",
      description: "Inject the selected scheme through managed Spicetify color variables.",
    },
    {
      key: "layout",
      label: "Layout",
      description: "Apply font, scale, spacing, and corner choices.",
    },
    {
      key: "effects",
      label: "Effects",
      description: "Apply the selected glass, eco, or flat rendering tier.",
    },
    {
      key: "accessibility",
      label: "Accessibility",
      description: "Keep visible focus, motion limits, and high-contrast rules active.",
    },
  ];
  const scaleRows: {
    key: keyof EngineState["appearance"]["scale"];
    label: string;
  }[] = [
    { key: "navigation", label: "Navigation scale" },
    { key: "content", label: "Content scale" },
    { key: "playbar", label: "Playbar scale" },
    { key: "rightSidebar", label: "Right panel scale" },
  ];

  return h(
    "div",
    { className: "librespot-panel", "data-librespot-panel": "look" },
    PanelIntro({
      eyebrow: "Appearance",
      title: "Look",
      body: "Build a look from independent palette, layout, effects, and accessibility layers. Every change below applies to the current page immediately.",
      action: h(
        "div",
        { className: "librespot-fps" },
        h("span", null, state.lastMeasuredFps === null ? "FPS probe pending" : `${state.lastMeasuredFps} FPS`),
        h("strong", null, state.effectsTier),
      ),
    }),
    Section({
      title: "Theme and scheme",
      description: "Prism is the built-in engine theme. Installed themes join this list when the desktop catalog is present.",
      children: h(
        Spicetify.React.Fragment,
        null,
        SelectRow({
          label: "Theme",
          description: "Layer source for this profile.",
          value: state.theme,
          options: [{ value: "Prism", label: "Prism" }],
          onChange: (value) => {
            update(
              properties,
              (draft) => {
                draft.theme = value;
              },
              `${value} theme applied`,
            );
          },
        }),
        schemeCards(properties),
      ),
    }),
    Section({
      title: "Layers",
      description: "Keep only the parts you want. The four layers can be mixed freely.",
      children: h(
        Spicetify.React.Fragment,
        null,
        ...layerLabels.map((layer) =>
          ToggleRow({
            label: layer.label,
            description: layer.description,
            checked: state.layers[layer.key],
            onChange: (checked) => {
              update(
                properties,
                (draft) => {
                  draft.layers[layer.key] = checked;
                },
                `${layer.label} layer ${checked ? "enabled" : "disabled"}`,
              );
            },
          }),
        ),
      ),
    }),
    Section({
      title: "Effects",
      description: "Glass uses blur, eco keeps translucency without blur, and flat removes both motion and blur.",
      children: h(
        Spicetify.React.Fragment,
        null,
        SegmentedControl({
          label: "Effects tier",
          value: state.effectsTier,
          options: [
            { value: "glass", label: "Glass" },
            { value: "eco", label: "Eco" },
            { value: "flat", label: "Flat" },
          ],
          onChange: (value) => {
            update(
              properties,
              (draft) => {
                draft.effectsTier = value as EngineState["effectsTier"];
              },
              `${value} effects applied`,
            );
          },
        }),
        ToggleRow({
          label: "Automatic performance step-down",
          description: "Use the one-second frame probe to lower expensive effects on slower rendering paths.",
          checked: state.autoEffects,
          onChange: (checked) => {
            update(
              properties,
              (draft) => {
                draft.autoEffects = checked;
              },
              checked ? "Automatic effects enabled" : "Manual effects selected",
            );
          },
        }),
      ),
    }),
    Section({
      title: "Dynamic accent",
      description: "Use the scheme, album artwork, a fixed color, or an OS signal supplied by LibreSpot.",
      children: h(
        Spicetify.React.Fragment,
        null,
        SegmentedControl({
          label: "Accent source",
          value: state.dynamicAccent.mode,
          options: [
            { value: "scheme", label: "Scheme" },
            { value: "album-art", label: "Album art" },
            { value: "fixed", label: "Fixed" },
            { value: "os", label: "OS" },
          ],
          onChange: (value) => {
            update(
              properties,
              (draft) => {
                draft.dynamicAccent.mode =
                  value as EngineState["dynamicAccent"]["mode"];
              },
              "Accent source updated",
            );
          },
        }),
        SelectRow({
          label: "Artwork swatch",
          description: "The colorExtractor result used for album-art accents.",
          value: state.dynamicAccent.preset,
          options: [
            { value: "VIBRANT", label: "Vibrant" },
            { value: "LIGHT_VIBRANT", label: "Light vibrant" },
            { value: "PROMINENT", label: "Prominent" },
          ],
          onChange: (value) => {
            update(
              properties,
              (draft) => {
                draft.dynamicAccent.preset =
                  value as EngineState["dynamicAccent"]["preset"];
              },
              "Artwork swatch updated",
            );
          },
        }),
        ToggleRow({
          label: "Material full-palette tint",
          description: "Derive surfaces and controls from the accent instead of changing only the highlight.",
          checked: state.dynamicAccent.materialPalette,
          onChange: (checked) => {
            update(
              properties,
              (draft) => {
                draft.dynamicAccent.materialPalette = checked;
              },
              checked ? "Material palette applied" : "Scheme palette restored",
            );
          },
        }),
      ),
    }),
    Section({
      title: "Type, corners, and scale",
      description: "Scale each Spotify region independently without zooming the whole client.",
      children: h(
        Spicetify.React.Fragment,
        null,
        SelectRow({
          label: "Font",
          description: "Uses a local font when it exists and falls back to Spotify Mix.",
          value: state.appearance.fontFamily,
          options: [
            {
              value: "SpotifyMixUI, CircularSp, sans-serif",
              label: "Spotify Mix",
            },
            {
              value: "Atkinson Hyperlegible, SpotifyMixUI, sans-serif",
              label: "Atkinson Hyperlegible",
            },
            {
              value: "system-ui, sans-serif",
              label: "System UI",
            },
          ],
          onChange: (value) => {
            update(
              properties,
              (draft) => {
                draft.appearance.fontFamily = value;
              },
              "Font updated",
            );
          },
        }),
        SliderRow({
          label: "Corner radius",
          description: "Shared radius for cards, controls, and artwork.",
          value: state.appearance.radius,
          min: 0,
          max: 24,
          step: 1,
          suffix: "px",
          onChange: (value) => {
            update(
              properties,
              (draft) => {
                draft.appearance.radius = value;
              },
              "Corner radius updated",
            );
          },
        }),
        ...scaleRows.map((row) =>
          SliderRow({
            label: row.label,
            description: "Independent region scale.",
            value: state.appearance.scale[row.key],
            min: 0.8,
            max: 1.3,
            step: 0.05,
            suffix: "×",
            onChange: (value) => {
              update(
                properties,
                (draft) => {
                  draft.appearance.scale[row.key] = value;
                },
                `${row.label} updated`,
              );
            },
          }),
        ),
      ),
    }),
    Section({
      title: "Schedule",
      description: "Light and dark changes follow the local clock and apply on the next minute tick.",
      children: scheduleControls(properties),
    }),
  );
}
