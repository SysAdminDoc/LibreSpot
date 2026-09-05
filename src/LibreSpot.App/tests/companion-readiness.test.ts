import { describe, expect, it } from "vitest";
import { isCompanionApiReady } from "../src/extensions/companion-readiness.ts";

const readyApi = {
  React: {},
  Platform: { History: {} },
  LocalStorage: {},
  Player: {},
};

describe("companion API readiness", () => {
  it("starts without the optional ReactDOM global used by older Spotify loads", () => {
    expect(isCompanionApiReady(readyApi)).toBe(true);
  });

  it.each([
    ["React", { ...readyApi, React: undefined }],
    ["history", { ...readyApi, Platform: {} }],
    ["local storage", { ...readyApi, LocalStorage: undefined }],
    ["player", { ...readyApi, Player: undefined }],
  ])("waits when %s is unavailable", (_name, api) => {
    expect(isCompanionApiReady(api)).toBe(false);
  });
});
