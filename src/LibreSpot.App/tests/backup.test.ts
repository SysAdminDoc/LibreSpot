import { describe, expect, it } from "vitest";
import {
  BACKUP_SCHEMA_VERSION,
  createBackup,
  parseBackup,
  indexedDbMarketplaceStore,
  serializeBackup,
  type MarketplaceEntries,
  type MarketplaceStore,
} from "../src/core/backup.ts";
import { EngineStore, type StorageAdapter } from "../src/core/store.ts";
import { createDefaultState, PROFILE_SCHEMA_VERSION } from "../src/core/state.ts";

function memoryStorage(): StorageAdapter {
  const map = new Map<string, string>();
  return {
    get: (key) => map.get(key) ?? null,
    set: (key, value) => {
      map.set(key, value);
    },
    remove: (key) => {
      map.delete(key);
    },
  };
}

function memoryMarketplace(seed: MarketplaceEntries = {}): MarketplaceStore & {
  entries: MarketplaceEntries;
} {
  const state: { entries: MarketplaceEntries } = { entries: { ...seed } };
  return {
    get entries() {
      return state.entries;
    },
    readAll: () => Promise.resolve({ available: true, entries: { ...state.entries } }),
    writeAll: (entries) => {
      state.entries = { ...state.entries, ...entries };
      return Promise.resolve();
    },
  };
}

function stateFixture(now: Date): ReturnType<typeof createDefaultState> {
  // createDefaultState leaves schemes empty; the engine fills them from the
  // catalog at runtime, and a profile without its active scheme is rejected.
  const state = createDefaultState(now);
  state.schemes = {
    Dark: { text: "FFFFFF", main: "000000", button: "1ED760", accent: "1ED760" },
    Light: { text: "111111", main: "FFFFFF", button: "16843D", accent: "16843D" },
  };
  return state;
}

const MARKETPLACE_SEED: MarketplaceEntries = {
  "marketplace:installed-extensions": ["owner/repo/main.js"],
  "marketplace:active-tab": "Extensions",
  "marketplace:tabs": ["Extensions", "Themes", "Snippets", "Apps"],
  "internal:local-storage-migrated": true,
};

describe("backup", () => {
  it("carries the engine state and Marketplace settings in one file", () => {
    const state = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    state.name = "Evening";

    const backup = createBackup(state, MARKETPLACE_SEED, new Date("2026-09-03T11:00:00.000Z"));

    expect(backup.schemaVersion).toBe(BACKUP_SCHEMA_VERSION);
    expect(backup.createdAt).toBe("2026-09-03T11:00:00.000Z");
    expect(backup.engine.name).toBe("Evening");
    expect(backup.marketplace).toEqual(MARKETPLACE_SEED);
    // The desktop imports the same envelope it already understands.
    expect(backup.profile).toMatchObject({ generator: "LibreSpot-Spotify" });
  });

  it("restores engine state and Marketplace settings after the profile is wiped", async () => {
    const storage = memoryStorage();
    const store = new EngineStore(storage, () => new Date("2026-09-03T10:00:00.000Z"));
    const marketplace = memoryMarketplace(MARKETPLACE_SEED);

    const saved = store.save({
      ...stateFixture(new Date("2026-09-03T10:00:00.000Z")),
      name: "Evening",
    });
    const file = serializeBackup(
      createBackup(saved, (await marketplace.readAll()).entries, new Date("2026-09-03T11:00:00.000Z")),
    );

    // Everything a cleared Spotify profile takes with it.
    store.reset();
    const wipedMarketplace = memoryMarketplace();
    expect(storage.get("librespot:engine-state")).toBeNull();
    expect((await wipedMarketplace.readAll()).entries).toEqual({});

    const restored = parseBackup(file);
    const reloaded = store.save(restored.engine);
    await wipedMarketplace.writeAll(restored.marketplace);

    expect(reloaded.name).toBe("Evening");
    expect(store.load().name).toBe("Evening");
    expect((await wipedMarketplace.readAll()).entries).toEqual(MARKETPLACE_SEED);
    expect(restored.createdAt).toBe("2026-09-03T11:00:00.000Z");
  });

  it("keeps every engine field through a round trip", () => {
    const state = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    state.name = "Round trip";
    state.enabledSnippets = ["hide-upgrade-button"];
    state.featureOverrides = { automix_enabled: true };

    const restored = parseBackup(
      serializeBackup(createBackup(state, {}, new Date("2026-09-03T11:00:00.000Z"))),
    );

    expect(restored.engine).toEqual(state);
  });

  it("reads and writes Marketplace records the way Marketplace stores them", async () => {
    // The settings store uses an in-line key at keyPath "key", so records are
    // { key, value } and put() must not be given a second argument.
    const records: unknown[] = [
      { key: "marketplace:active-tab", value: "Themes" },
      { key: "internal:local-storage-migrated", value: "1" },
    ];
    const puts: unknown[] = [];
    let putThrew: string | null = null;

    const fakeFactory = {
      open: () => {
        const request: Record<string, unknown> = { result: null };
        queueMicrotask(() => {
          const store = {
            getAll: () => ({ result: records }),
            put: (record: unknown, key?: unknown) => {
              if (key !== undefined) {
                putThrew = "put received an explicit key";
                throw new Error("in-line keys reject an explicit key");
              }
              puts.push(record);
            },
          };
          const transaction: Record<string, unknown> = {
            objectStore: () => store,
          };
          request.result = {
            objectStoreNames: { contains: () => true },
            transaction: () => transaction,
            close: () => undefined,
          };
          (request.onsuccess as () => void)();
          queueMicrotask(() => {
            (transaction.oncomplete as () => void)();
          });
        });
        return request as unknown as IDBOpenDBRequest;
      },
    } as unknown as IDBFactory;

    const store = indexedDbMarketplaceStore(fakeFactory, 200);

    const read = await store.readAll();
    expect(read.available).toBe(true);
    expect({ ...read.entries }).toEqual({
      "marketplace:active-tab": "Themes",
      "internal:local-storage-migrated": "1",
    });

    await store.writeAll({ "marketplace:active-tab": "Extensions" });
    expect(putThrew).toBeNull();
    expect(puts).toEqual([{ key: "marketplace:active-tab", value: "Extensions" }]);
  });

  it("gives up on a Marketplace database that never opens", async () => {
    // A blocked or stalled open used to hang the panel button forever.
    const stalled: IDBFactory = {
      open: () => ({}) as IDBOpenDBRequest,
    } as unknown as IDBFactory;

    const store = indexedDbMarketplaceStore(stalled, 20);

    // An unreadable database must report itself, not look like an empty one.
    const read = await store.readAll();
    expect(read.available).toBe(false);
    expect(read.entries).toEqual({});
    await expect(store.writeAll({ a: 1 })).rejects.toThrow(/not available/);
  });

  it("refuses a file that is not a backup", () => {
    expect(() => parseBackup("{}")).toThrow(/schemaVersion/);
    expect(() => parseBackup(JSON.stringify({ schemaVersion: 1 }))).toThrow(/engine state/);
    expect(() =>
      parseBackup(JSON.stringify({ schemaVersion: BACKUP_SCHEMA_VERSION + 1, engine: {} })),
    ).toThrow(/newer LibreSpot/);
    expect(() =>
      parseBackup(
        JSON.stringify({
          schemaVersion: BACKUP_SCHEMA_VERSION,
          engine: { schemaVersion: PROFILE_SCHEMA_VERSION + 1 },
        }),
      ),
    ).toThrow(/schema/);
  });

  it("keeps a __proto__ key as data instead of losing it", () => {
    const state = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    // Written as raw JSON on purpose: in an object literal "__proto__:" sets the
    // prototype and the key never reaches the file at all.
    const file = `{"schemaVersion": ${BACKUP_SCHEMA_VERSION}, "engine": ${JSON.stringify(state)}, "marketplace": {"__proto__": {"polluted": true}, "real": 1}}`;

    const restored = parseBackup(file);

    expect(Object.keys(restored.marketplace).sort()).toEqual(["__proto__", "real"]);
    expect(({} as Record<string, unknown>).polluted).toBeUndefined();
  });

  it("treats a missing Marketplace section as nothing to restore", () => {
    const state = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    const file = JSON.stringify({
      schemaVersion: BACKUP_SCHEMA_VERSION,
      engine: state,
    });

    expect(parseBackup(file).marketplace).toEqual({});
  });
});
