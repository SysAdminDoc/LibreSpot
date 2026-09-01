import type {
  LibreSpotRuntimeApi,
  LibreSpotRuntimeSnapshot,
  UiNode,
} from "../spicetify-globals.d.ts";

export type PanelProperties = {
  runtime: LibreSpotRuntimeApi;
  snapshot: LibreSpotRuntimeSnapshot;
};

export type PanelComponent = (properties: PanelProperties) => UiNode;
