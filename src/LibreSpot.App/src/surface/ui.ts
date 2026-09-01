import type { UiElementType, UiNode } from "../spicetify-globals.d.ts";

export function h<Properties extends object>(
  type: string | ((properties: Properties) => UiNode),
  properties?: Properties | null,
  ...children: UiNode[]
): UiNode {
  return Spicetify.React.createElement(
    type as UiElementType,
    (properties ?? null) as Record<string, unknown> | null,
    ...children,
  );
}

function targetValue(event: unknown): string {
  if (!(event instanceof Event)) {
    return "";
  }
  const target = event.target;
  return target instanceof HTMLInputElement || target instanceof HTMLSelectElement
    ? target.value
    : "";
}

export function PanelIntro(properties: {
  eyebrow: string;
  title: string;
  body: string;
  action?: UiNode;
}): UiNode {
  return h(
    "header",
    { className: "librespot-panel-intro" },
    h(
      "div",
      { className: "librespot-panel-intro__copy" },
      h("span", { className: "librespot-eyebrow" }, properties.eyebrow),
      h("h1", null, properties.title),
      h("p", null, properties.body),
    ),
    properties.action ?? null,
  );
}

export function Section(properties: {
  title: string;
  description?: string;
  children?: UiNode;
  className?: string;
}): UiNode {
  return h(
    "section",
    {
      className: `librespot-section ${properties.className ?? ""}`.trim(),
    },
    h(
      "div",
      { className: "librespot-section__heading" },
      h("h2", null, properties.title),
      properties.description ? h("p", null, properties.description) : null,
    ),
    h("div", { className: "librespot-section__body" }, properties.children),
  );
}

export function ToggleRow(properties: {
  label: string;
  description: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  disabled?: boolean;
  badge?: string;
}): UiNode {
  return h(
    "div",
    { className: "librespot-control-row" },
    h(
      "div",
      { className: "librespot-control-copy" },
      h(
        "div",
        { className: "librespot-control-label" },
        h("span", null, properties.label),
        properties.badge
          ? h("span", { className: "librespot-badge" }, properties.badge)
          : null,
      ),
      h("p", null, properties.description),
    ),
    h(
      "button",
      {
        type: "button",
        className: "librespot-switch",
        role: "switch",
        "aria-checked": String(properties.checked),
        "aria-label": properties.label,
        disabled: properties.disabled ?? false,
        onClick: () => {
          properties.onChange(!properties.checked);
        },
      },
      h("span", { className: "librespot-switch__thumb" }),
    ),
  );
}

export function SelectRow(properties: {
  label: string;
  description: string;
  value: string;
  options: readonly { value: string; label: string }[];
  onChange: (value: string) => void;
}): UiNode {
  return h(
    "label",
    { className: "librespot-control-row" },
    h(
      "div",
      { className: "librespot-control-copy" },
      h("span", { className: "librespot-control-label" }, properties.label),
      h("p", null, properties.description),
    ),
    h(
      "select",
      {
        className: "librespot-select",
        value: properties.value,
        onChange: (event: unknown) => {
          properties.onChange(targetValue(event));
        },
      },
      ...properties.options.map((option) =>
        h("option", { value: option.value, key: option.value }, option.label),
      ),
    ),
  );
}

export function SliderRow(properties: {
  label: string;
  description: string;
  value: number;
  min: number;
  max: number;
  step: number;
  suffix?: string;
  onChange: (value: number) => void;
}): UiNode {
  return h(
    "label",
    { className: "librespot-control-row" },
    h(
      "div",
      { className: "librespot-control-copy" },
      h("span", { className: "librespot-control-label" }, properties.label),
      h("p", null, properties.description),
    ),
    h(
      "div",
      { className: "librespot-slider-wrap" },
      h("span", { className: "librespot-value" }, `${properties.value}${properties.suffix ?? ""}`),
      h("input", {
        type: "range",
        value: String(properties.value),
        min: String(properties.min),
        max: String(properties.max),
        step: String(properties.step),
        "aria-label": properties.label,
        onChange: (event: unknown) => {
          properties.onChange(Number(targetValue(event)));
        },
      }),
    ),
  );
}

export function SegmentedControl(properties: {
  label: string;
  value: string;
  options: readonly { value: string; label: string }[];
  onChange: (value: string) => void;
}): UiNode {
  return h(
    "div",
    {
      className: "librespot-segmented",
      role: "group",
      "aria-label": properties.label,
    },
    ...properties.options.map((option) =>
      h(
        "button",
        {
          type: "button",
          key: option.value,
          className:
            option.value === properties.value
              ? "librespot-segmented__button is-active"
              : "librespot-segmented__button",
          "aria-pressed": String(option.value === properties.value),
          onClick: () => {
            properties.onChange(option.value);
          },
        },
        option.label,
      ),
    ),
  );
}

export function ActionButton(properties: {
  label: string;
  onClick: () => void;
  secondary?: boolean;
  disabled?: boolean;
}): UiNode {
  return h(
    "button",
    {
      type: "button",
      className: properties.secondary
        ? "librespot-button librespot-button--secondary"
        : "librespot-button",
      disabled: properties.disabled ?? false,
      onClick: properties.onClick,
    },
    properties.label,
  );
}
