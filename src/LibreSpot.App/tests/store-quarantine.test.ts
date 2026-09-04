import {
  createDefaultState,
  EngineStore,
  ENGINE_STORAGE_KEY,
  PROFILE_SCHEMA_VERSION,
  QUARANTINE_KEY_PREFIX,
  QUARANTINE_POINTER_KEY,
  serializeEngineState,
  type StorageAdapter,
} from "../src/core/index.ts";

function memoryStorage(): StorageAdapter & { snapshot(): Map<string, string> } {
  const values = new Map<string, string>();
  return {
    get: (key) => values.get(key) ?? null,
    set: (key, value) => {
      values.set(key, value);
    },
    remove: (key) => {
      values.delete(key);
    },
    snapshot: () => new Map(values),
  };
}

const QUARANTINED_AT = "2026-09-04T10:00:00.000Z";

function storeAt(storage: StorageAdapter, iso = QUARANTINED_AT): EngineStore {
  return new EngineStore(storage, () => new Date(iso));
}

describe("engine state quarantine", () => {
  it("keeps a state from a newer schema instead of deleting it", () => {
    const storage = memoryStorage();
    const future = JSON.stringify({
      schemaVersion: 99,
      name: "Mine",
      theme: "Prism",
      scheme: "Dark",
      schemes: { Dark: { main: "000000", text: "FFFFFF", accent: "1ED760" } },
    });
    storage.set(ENGINE_STORAGE_KEY, future);

    const state = storeAt(storage).load();

    expect(state.schemaVersion).toBe(PROFILE_SCHEMA_VERSION);
    expect(storage.get(ENGINE_STORAGE_KEY)).toBeNull();

    const kept = storeAt(storage).readQuarantine();
    expect(kept).not.toBeNull();
    expect(kept?.key).toBe(`${QUARANTINE_KEY_PREFIX}${QUARANTINED_AT}`);
    expect(kept?.quarantinedAt).toBe(QUARANTINED_AT);
    expect(kept?.raw).toBe(future);
    expect(kept?.reason).toContain("99");
  });

  it("keeps a truncated state instead of deleting it", () => {
    const storage = memoryStorage();
    const truncated = serializeEngineState(
      createDefaultState(new Date(QUARANTINED_AT)),
    ).slice(0, 40);
    storage.set(ENGINE_STORAGE_KEY, truncated);

    const state = storeAt(storage).load();

    expect(state.schemaVersion).toBe(PROFILE_SCHEMA_VERSION);
    expect(storage.get(ENGINE_STORAGE_KEY)).toBeNull();
    expect(storeAt(storage).readQuarantine()?.raw).toBe(truncated);
  });

  it("retains only the newest unreadable state", () => {
    const storage = memoryStorage();
    storage.set(ENGINE_STORAGE_KEY, "{ broken");
    storeAt(storage, "2026-09-01T00:00:00.000Z").load();

    storage.set(ENGINE_STORAGE_KEY, "{ broken again");
    storeAt(storage, "2026-09-02T00:00:00.000Z").load();

    const dated = [...storage.snapshot().keys()].filter((key) =>
      key.startsWith(QUARANTINE_KEY_PREFIX),
    );
    expect(dated).toEqual([`${QUARANTINE_KEY_PREFIX}2026-09-02T00:00:00.000Z`]);
    expect(storeAt(storage).readQuarantine()?.raw).toBe("{ broken again");
  });

  it("reports nothing to recover once the copy is discarded", () => {
    const storage = memoryStorage();
    storage.set(ENGINE_STORAGE_KEY, "{ broken");
    const store = storeAt(storage);
    store.load();
    expect(store.readQuarantine()).not.toBeNull();

    store.discardQuarantine();

    expect(store.readQuarantine()).toBeNull();
    expect(storage.get(QUARANTINE_POINTER_KEY)).toBeNull();
    expect(
      [...storage.snapshot().keys()].filter((key) =>
        key.startsWith(QUARANTINE_KEY_PREFIX),
      ),
    ).toEqual([]);
  });

  it("keeps the unreadable state in place when the copy cannot be written", () => {
    // A profile's storage is shared with Spotify's own keys, so a large state
    // plus its copy can exceed the quota. Losing the original there would be
    // the exact failure this feature exists to prevent.
    const map = new Map<string, string>();
    map.set(ENGINE_STORAGE_KEY, "{ broken");
    const full: StorageAdapter = {
      get: (key) => map.get(key) ?? null,
      set: (key) => {
        throw new Error(`QuotaExceededError writing ${key}`);
      },
      remove: (key) => {
        map.delete(key);
      },
    };

    const store = storeAt(full);
    expect(store.load().schemaVersion).toBe(PROFILE_SCHEMA_VERSION);

    expect(map.get(ENGINE_STORAGE_KEY)).toBe("{ broken");
    expect(store.readQuarantine()).toBeNull();
  });

  it("keeps the older copy when the newer one cannot be written", () => {
    const storage = memoryStorage();
    storage.set(ENGINE_STORAGE_KEY, "{ first broken");
    storeAt(storage, "2026-09-01T00:00:00.000Z").load();
    const firstKey = storage.get(QUARANTINE_POINTER_KEY);
    expect(firstKey).not.toBeNull();

    const failing: StorageAdapter = {
      get: (key) => storage.get(key),
      set: (key, value) => {
        if (key.startsWith(QUARANTINE_KEY_PREFIX)) {
          throw new Error("QuotaExceededError");
        }
        storage.set(key, value);
      },
      remove: (key) => {
        storage.remove(key);
      },
    };
    storage.set(ENGINE_STORAGE_KEY, "{ second broken");
    new EngineStore(failing, () => new Date("2026-09-02T00:00:00.000Z")).load();

    expect(storage.get(QUARANTINE_POINTER_KEY)).toBe(firstKey);
    expect(storeAt(storage).readQuarantine()?.raw).toBe("{ first broken");
    expect(storage.get(ENGINE_STORAGE_KEY)).toBe("{ second broken");
  });

  it("refuses to discard through a pointer that names the live state", () => {
    const storage = memoryStorage();
    const saved = serializeEngineState(createDefaultState(new Date(QUARANTINED_AT)));
    storage.set(ENGINE_STORAGE_KEY, saved);
    storage.set(QUARANTINE_POINTER_KEY, ENGINE_STORAGE_KEY);

    storeAt(storage).discardQuarantine();

    expect(storage.get(ENGINE_STORAGE_KEY)).toBe(saved);
    expect(storage.get(QUARANTINE_POINTER_KEY)).toBeNull();
  });

  it("leaves a readable state alone", () => {
    const storage = memoryStorage();
    // createDefaultState leaves schemes empty and parseProfile requires the
    // selected scheme to exist, so a readable fixture has to name one.
    const readable = createDefaultState(new Date(QUARANTINED_AT));
    readable.schemes = {
      [readable.scheme]: { main: "000000", text: "FFFFFF", accent: "1ED760" },
    };
    const saved = serializeEngineState(readable);
    storage.set(ENGINE_STORAGE_KEY, saved);

    const store = storeAt(storage);
    store.load();

    expect(storage.get(ENGINE_STORAGE_KEY)).toBe(saved);
    expect(store.readQuarantine()).toBeNull();
  });
});
