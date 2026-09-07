import { describe, expect, it } from "vitest";
import { isCompanionApiReady } from "../src/extensions/companion-readiness.ts";

const readyApi = {
  React: {
    Fragment: Symbol("Fragment"),
    createElement: () => undefined,
    useCallback: () => undefined,
    useEffect: () => undefined,
    useMemo: () => undefined,
    useState: () => [undefined, () => undefined],
  },
  Platform: {
    History: {
      location: { pathname: "/librespot" },
      push: () => undefined,
    },
  },
  LocalStorage: {
    get: () => null,
    set: () => undefined,
  },
  Player: {
    addEventListener: () => undefined,
  },
};

describe("companion API readiness", () => {
  it("accepts the fully initialized companion without optional ReactDOM or history listeners", () => {
    expect(isCompanionApiReady(readyApi)).toBe(true);
  });

  it.each([
    ["React.createElement", { ...readyApi, React: { ...readyApi.React, createElement: undefined } }],
    ["React.useState", { ...readyApi, React: { ...readyApi.React, useState: undefined } }],
    ["history location", { ...readyApi, Platform: { History: { ...readyApi.Platform.History, location: undefined } } }],
    ["history.push", { ...readyApi, Platform: { History: { ...readyApi.Platform.History, push: undefined } } }],
    ["local storage get", { ...readyApi, LocalStorage: { ...readyApi.LocalStorage, get: undefined } }],
    ["local storage set", { ...readyApi, LocalStorage: { ...readyApi.LocalStorage, set: undefined } }],
    ["player.addEventListener", { ...readyApi, Player: { ...readyApi.Player, addEventListener: undefined } }],
  ])("waits when %s is unavailable", (_name, api) => {
    expect(isCompanionApiReady(api)).toBe(false);
  });

  it("stays false while the companion publishes its required surfaces in stages", () => {
    const staged: Record<string, unknown> = {};
    expect(isCompanionApiReady(staged)).toBe(false);

    staged.React = readyApi.React;
    expect(isCompanionApiReady(staged)).toBe(false);
    staged.Platform = { History: readyApi.Platform.History };
    expect(isCompanionApiReady(staged)).toBe(false);
    staged.LocalStorage = readyApi.LocalStorage;
    expect(isCompanionApiReady(staged)).toBe(false);
    staged.Player = readyApi.Player;

    expect(isCompanionApiReady(staged)).toBe(true);
  });
});
