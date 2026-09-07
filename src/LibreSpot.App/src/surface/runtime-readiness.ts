import type { LibreSpotRuntimeApi } from "../spicetify-globals.d.ts";

export function readReadyRuntime(host: Window = window): LibreSpotRuntimeApi | null {
  if (!host.__libreSpotEngineLoaded) {
    return null;
  }
  return host.LibreSpot ?? null;
}
