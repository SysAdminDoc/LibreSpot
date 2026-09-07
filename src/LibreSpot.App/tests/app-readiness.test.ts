import { readReadyRuntime } from "../src/surface/runtime-readiness.ts";
import type { LibreSpotRuntimeApi } from "../src/spicetify-globals.d.ts";

describe("in-client runtime readiness", () => {
  afterEach(() => {
    Reflect.deleteProperty(window, "LibreSpot");
    Reflect.deleteProperty(window, "__libreSpotEngineLoaded");
  });

  it("rejects a runtime that was published before startup completed", () => {
    const failedRuntime = {} as LibreSpotRuntimeApi;
    window.LibreSpot = failedRuntime;
    window.__libreSpotEngineLoaded = false;

    expect(readReadyRuntime()).toBeNull();
  });

  it("binds the replacement runtime after a retry completes", () => {
    const replacementRuntime = {} as LibreSpotRuntimeApi;
    window.LibreSpot = replacementRuntime;
    window.__libreSpotEngineLoaded = true;

    expect(readReadyRuntime()).toBe(replacementRuntime);
  });
});
