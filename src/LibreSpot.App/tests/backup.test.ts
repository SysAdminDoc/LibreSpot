import { describe, expect, it } from "vitest";
import {
  BACKUP_SCHEMA_VERSION,
  MARKETPLACE_MIGRATION_KEY,
  MAX_MARKETPLACE_BYTES,
  createBackup,
  parseBackup,
  parseRestoreSource,
  indexedDbMarketplaceStore,
  restoreBackupTransaction,
  RestoreTransactionError,
  serializeBackup,
  type MarketplaceEntries,
  type MarketplaceStore,
} from "../src/core/backup.ts";
import {
  ENGINE_STORAGE_KEY,
  EngineStore,
  MAX_RECOVERY_RECORD_BYTES,
  type RecoveryRecord,
  type StorageAdapter,
} from "../src/core/store.ts";
import { createDefaultState, PROFILE_SCHEMA_VERSION } from "../src/core/state.ts";
import { MAX_PROFILE_BYTES } from "../src/core/profile.ts";

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
    restoreKeys: (entries, keys) => {
      const next = { ...state.entries };
      for (const key of keys) {
        if (Object.prototype.hasOwnProperty.call(entries, key)) {
          next[key] = entries[key];
        } else {
          Reflect.deleteProperty(next, key);
        }
      }
      state.entries = next;
      return Promise.resolve();
    },
    deleteAll: () => {
      state.entries = {};
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

type FakeMarketplaceRequest<T> = {
  result: T;
  onsuccess: (() => void) | null;
  onerror: (() => void) | null;
  onblocked: (() => void) | null;
  onupgradeneeded?: (() => void) | null;
};

function fakeWebStorage(seed: Record<string, string> = {}) {
  const values = new Map(Object.entries(seed));
  return {
    get length() {
      return values.size;
    },
    key: (index: number) => [...values.keys()][index] ?? null,
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => {
      values.set(key, value);
    },
    removeItem: (key: string) => {
      values.delete(key);
    },
    entries: () => Object.fromEntries(values),
  } as unknown as Storage & { entries(): Record<string, string> };
}

function fakeMarketplaceIndexedDb(seed: unknown[] = []) {
  const records = [...seed];
  let deleteCalls = 0;
  let completeScheduled = false;
  let transaction: {
    objectStore: () => {
      getAll: () => FakeMarketplaceRequest<unknown[]>;
      put: (record: unknown) => unknown;
      delete: (key: string) => unknown;
    };
    oncomplete: (() => void) | null;
    onerror: (() => void) | null;
    onabort: (() => void) | null;
  };

  const scheduleComplete = () => {
    if (completeScheduled) return;
    completeScheduled = true;
    queueMicrotask(() => transaction.oncomplete?.());
  };

  const createTransaction = () => {
    completeScheduled = false;
    const store = {
      getAll: () => {
        const request: FakeMarketplaceRequest<unknown[]> = {
          result: [...records],
          onsuccess: null,
          onerror: null,
          onblocked: null,
        };
        queueMicrotask(() => {
          request.onsuccess?.();
          scheduleComplete();
        });
        return request;
      },
      put: (record: unknown) => {
        const value = record as { key?: unknown };
        const index = records.findIndex(
          (candidate) =>
            typeof candidate === "object" &&
            candidate !== null &&
            (candidate as { key?: unknown }).key === value.key,
        );
        if (index >= 0) records[index] = record;
        else records.push(record);
        scheduleComplete();
        return {};
      },
      delete: (key: string) => {
        const index = records.findIndex(
          (candidate) =>
            typeof candidate === "object" &&
            candidate !== null &&
            (candidate as { key?: unknown }).key === key,
        );
        if (index >= 0) records.splice(index, 1);
        scheduleComplete();
        return {};
      },
    };
    transaction = {
      objectStore: () => store,
      oncomplete: null,
      onerror: null,
      onabort: null,
    };
    return transaction;
  };

  const factory: IDBFactory = {
    open: () => {
      const request: FakeMarketplaceRequest<IDBDatabase | null> = {
        result: null,
        onsuccess: null,
        onerror: null,
        onblocked: null,
        onupgradeneeded: null,
      };
      queueMicrotask(() => {
        request.result = {
          objectStoreNames: { contains: () => true },
          transaction: () => createTransaction(),
          close: () => undefined,
        } as unknown as IDBDatabase;
        request.onsuccess?.();
      });
      return request as unknown as IDBOpenDBRequest;
    },
    deleteDatabase: () => {
      deleteCalls += 1;
      const request: FakeMarketplaceRequest<undefined> = {
        result: undefined,
        onsuccess: null,
        onerror: null,
        onblocked: null,
      };
      queueMicrotask(() => {
        records.length = 0;
        request.onsuccess?.();
      });
      return request as unknown as IDBOpenDBRequest;
    },
  } as unknown as IDBFactory;

  return {
    factory,
    records,
    get deleteCalls() {
      return deleteCalls;
    },
  };
}

function restoreEngineFixture(initial: ReturnType<typeof stateFixture>, storage: StorageAdapter) {
  let current = structuredClone(initial);
  let failTargetWrite = false;
  let failExactRestore = false;
  return {
    get state() {
      return structuredClone(current);
    },
    readPersistedRaw() {
      return storage.get(ENGINE_STORAGE_KEY);
    },
    set failTargetWrite(value: boolean) {
      failTargetWrite = value;
    },
    set failExactRestore(value: boolean) {
      failExactRestore = value;
    },
    replace(next: ReturnType<typeof stateFixture>) {
      current = structuredClone(next);
      storage.set(ENGINE_STORAGE_KEY, JSON.stringify(current));
      if (failTargetWrite && next.name === "Restored") {
        throw new Error("engine write failed after commit");
      }
      return structuredClone(current);
    },
    restoreExact(next: ReturnType<typeof stateFixture>, persistedRaw?: string | null) {
      if (failExactRestore) {
        throw new Error("engine compensation failed");
      }
      current = structuredClone(next);
      if (persistedRaw === null) {
        storage.remove(ENGINE_STORAGE_KEY);
      } else {
        storage.set(
          ENGINE_STORAGE_KEY,
          persistedRaw ?? JSON.stringify(current),
        );
      }
      return structuredClone(current);
    },
    refreshAccent: () => Promise.resolve(),
    applyFlags: () => Promise.resolve("debug-api" as const),
  };
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

  it("rejects oversized Marketplace payloads and raw restore sources", () => {
    const state = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    const oversizedMarketplace = {
      ...createBackup(state, {}, new Date("2026-09-03T11:00:00.000Z")),
      marketplace: { payload: "x".repeat(MAX_MARKETPLACE_BYTES) },
    };

    expect(() => parseBackup(serializeBackup(oversizedMarketplace))).toThrow(
      /Marketplace settings exceed/,
    );
    expect(() => parseRestoreSource("x".repeat(MAX_PROFILE_BYTES + 1))).toThrow(
      /profile exceeds/,
    );
  });

  it("reads and writes Marketplace records the way Marketplace stores them", async () => {
    // The settings store uses an in-line key at keyPath "key", so records are
    // { key, value } and put() must not be given a second argument.
    const records: unknown[] = [
      { key: "marketplace:active-tab", value: "Themes" },
      { key: "internal:local-storage-migrated", value: "1" },
    ];
    const puts: unknown[] = [];
    const deletes: unknown[] = [];
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
            delete: (key: unknown) => {
              deletes.push(key);
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
    });

    await store.writeAll({ "marketplace:active-tab": "Extensions" });
    expect(putThrew).toBeNull();
    expect(puts).toEqual([{ key: "marketplace:active-tab", value: "Extensions" }]);

    await store.restoreKeys(
      { "marketplace:active-tab": "Themes" },
      ["marketplace:active-tab", "marketplace:introduced"],
    );
    expect(puts.at(-1)).toEqual({ key: "marketplace:active-tab", value: "Themes" });
    expect(deletes).toEqual(["marketplace:introduced"]);
  });

  it("creates the known Marketplace schema for explicit reset recovery", async () => {
    let hasStore = false;
    let created = false;
    let aborted = false;
    const records: unknown[] = [];
    const recordsStore = {
      getAll: () => ({ result: records }),
      put: (record: unknown) => {
        const value = record as { key: string; value: unknown };
        const index = records.findIndex(
          (item) =>
            typeof item === "object" &&
            item !== null &&
            (item as { key?: unknown }).key === value.key,
        );
        if (index >= 0) records[index] = record;
        else records.push(record);
      },
      delete: () => undefined,
    };
    const factory = {
      open: () => {
        const request: Record<string, unknown> = {
          result: null,
          transaction: null,
        };
        queueMicrotask(() => {
          const transaction: Record<string, unknown> = {
            objectStore: () => recordsStore,
            abort: () => {
              aborted = true;
            },
          };
          const database = {
            objectStoreNames: { contains: () => hasStore },
            createObjectStore: () => {
              hasStore = true;
              created = true;
              return recordsStore;
            },
            transaction: () => transaction,
            close: () => undefined,
          };
          request.result = database;
          request.transaction = transaction;
          if (!hasStore) {
            (request.onupgradeneeded as () => void)();
          }
          if (!aborted) {
            (request.onsuccess as () => void)();
            queueMicrotask(() => {
              const oncomplete = transaction.oncomplete as (() => void) | undefined;
              oncomplete?.();
            });
          }
        });
        return request as unknown as IDBOpenDBRequest;
      },
    } as unknown as IDBFactory;

    const marketplace = indexedDbMarketplaceStore(factory, 200);
    const read = await marketplace.readAll(true);

    expect(read).toMatchObject({ available: true, entries: {} });
    expect(created).toBe(true);
    expect(aborted).toBe(false);

    await marketplace.writeAll({ "marketplace:active-tab": "Themes" });
    expect(records).toEqual([
      { key: "marketplace:active-tab", value: "Themes" },
    ]);
  });

  it("reads only Marketplace keys and gives IndexedDB precedence before migration", async () => {
    const legacy = fakeWebStorage({
      "marketplace:active-tab": "Legacy",
      "marketplace:legacy-only": "legacy",
      "spotify:unrelated": "keep",
    });
    const indexed = fakeMarketplaceIndexedDb([
      { key: "marketplace:active-tab", value: "Database" },
      { key: "internal:other", value: "ignore" },
    ]);
    const store = indexedDbMarketplaceStore(indexed.factory, 200, legacy);

    const beforeMigration = await store.readAll();
    expect(beforeMigration.entries).toEqual({
      "marketplace:active-tab": "Database",
      "marketplace:legacy-only": "legacy",
    });
    expect(beforeMigration.storage).toMatchObject({
      indexedDb: { "marketplace:active-tab": "Database" },
      localStorage: {
        "marketplace:active-tab": "Legacy",
        "marketplace:legacy-only": "legacy",
      },
      indexedDbAvailable: true,
      localStorageAvailable: true,
      indexedDbMigrationComplete: false,
    });
    expect(beforeMigration.entries).not.toHaveProperty("internal:other");
    expect(beforeMigration.entries).not.toHaveProperty("spotify:unrelated");

    indexed.records.push({ key: MARKETPLACE_MIGRATION_KEY, value: "1" });
    const afterMigration = await store.readAll();
    expect(afterMigration.entries).toEqual({
      "marketplace:active-tab": "Database",
    });
    expect(afterMigration.storage?.indexedDbMigrationComplete).toBe(true);
    expect(legacy.entries()).toEqual({
      "marketplace:active-tab": "Legacy",
      "marketplace:legacy-only": "legacy",
      "spotify:unrelated": "keep",
    });
  });

  it("uses Marketplace localStorage when IndexedDB is unavailable and clears only owned keys", async () => {
    const legacy = fakeWebStorage({
      "marketplace:active-tab": "Themes",
      "marketplace:theme-installed": "legacy-theme",
      "spotify:unrelated": "keep",
      "librespot:profile": "keep",
    });
    const unavailable: IDBFactory = {
      open: () => {
        throw new Error("IndexedDB disabled");
      },
    } as unknown as IDBFactory;
    const store = indexedDbMarketplaceStore(unavailable, 20, legacy);

    const read = await store.readAll();
    expect(read).toMatchObject({
      available: true,
      entries: {
        "marketplace:active-tab": "Themes",
        "marketplace:theme-installed": "legacy-theme",
      },
    });
    expect(read.storage).toMatchObject({
      indexedDbAvailable: false,
      localStorageAvailable: true,
    });

    await store.writeAll({ "marketplace:active-tab": "Extensions" });
    expect(legacy.getItem("marketplace:active-tab")).toBe("Extensions");
    await store.deleteAll();
    expect(legacy.entries()).toEqual({
      "spotify:unrelated": "keep",
      "librespot:profile": "keep",
    });
  });

  it("does not clear an unavailable captured backend as if it were empty", async () => {
    const legacy = fakeWebStorage({
      "marketplace:old": "before",
      "spotify:unrelated": "keep",
    });
    const indexed = fakeMarketplaceIndexedDb([
      { key: "marketplace:active-tab", value: "Database" },
      { key: MARKETPLACE_MIGRATION_KEY, value: "1" },
      { key: "internal:other", value: "keep" },
    ]);
    const store = indexedDbMarketplaceStore(indexed.factory, 200, legacy);

    await store.writeSnapshot({
      indexedDb: {},
      localStorage: { "marketplace:restored": "fallback" },
      indexedDbAvailable: false,
      localStorageAvailable: true,
      indexedDbMigrationComplete: false,
    });

    expect(indexed.records).toEqual([
      { key: "marketplace:active-tab", value: "Database" },
      { key: MARKETPLACE_MIGRATION_KEY, value: "1" },
      { key: "internal:other", value: "keep" },
    ]);
    expect(legacy.entries()).toEqual({
      "marketplace:restored": "fallback",
      "spotify:unrelated": "keep",
    });
  });

  it("refuses to restore a captured database when the target is unavailable", async () => {
    const unavailable: IDBFactory = {
      open: () => {
        throw new Error("IndexedDB disabled");
      },
    } as unknown as IDBFactory;
    const legacy = fakeWebStorage({ "spotify:unrelated": "keep" });
    const store = indexedDbMarketplaceStore(unavailable, 20, legacy);

    await expect(
      store.writeSnapshot({
        indexedDb: { "marketplace:active-tab": "Themes" },
        localStorage: {},
        indexedDbAvailable: true,
        localStorageAvailable: true,
        indexedDbMigrationComplete: true,
      }),
    ).rejects.toThrow(/database is not available/);
    expect(legacy.entries()).toEqual({ "spotify:unrelated": "keep" });
  });

  it("round-trips a migration snapshot without resurrecting old themes after reset", async () => {
    const legacy = fakeWebStorage({
      "marketplace:active-tab": "Legacy",
      "marketplace:old-theme": "theme-before-migration",
      "spotify:unrelated": "keep",
      "librespot:profile": "keep",
    });
    const indexed = fakeMarketplaceIndexedDb([
      { key: "marketplace:active-tab", value: "Database" },
      { key: MARKETPLACE_MIGRATION_KEY, value: "1" },
      { key: "internal:other", value: "ignore" },
    ]);
    const store = indexedDbMarketplaceStore(indexed.factory, 200, legacy);
    const captured = await store.readAll();
    const state = stateFixture(new Date("2026-09-06T22:00:00.000Z"));
    const parsed = parseBackup(
      serializeBackup(
        createBackup(
          state,
          captured.entries,
          new Date("2026-09-06T22:00:00.000Z"),
          captured.storage,
        ),
      ),
    );

    expect(parsed.marketplace).toEqual({
      "marketplace:active-tab": "Database",
    });
    expect(parsed.marketplaceStorage?.indexedDbMigrationComplete).toBe(true);
    await store.deleteAll();

    const reloaded = indexedDbMarketplaceStore(indexed.factory, 200, legacy);
    await expect(reloaded.readAll()).resolves.toMatchObject({
      available: true,
      entries: {},
    });
    expect(legacy.entries()).toEqual({
      "spotify:unrelated": "keep",
      "librespot:profile": "keep",
    });

    const marketplaceStorage = parsed.marketplaceStorage;
    if (!marketplaceStorage) throw new Error("The backup storage snapshot was not retained.");
    await reloaded.writeSnapshot(marketplaceStorage, true);
    const restored = await reloaded.readAll();
    expect(restored.entries).toEqual({
      "marketplace:active-tab": "Database",
    });
    expect(legacy.entries()).toEqual({
      "marketplace:active-tab": "Legacy",
      "marketplace:old-theme": "theme-before-migration",
      "spotify:unrelated": "keep",
      "librespot:profile": "keep",
    });
  });

  it("compensates exact Marketplace keys while preserving the merge boundary", async () => {
    const marketplace = memoryMarketplace({
      keep: "untouched",
      changed: "before",
    });

    await marketplace.writeAll({ changed: "after", introduced: true });
    await marketplace.restoreKeys(
      { changed: "before" },
      ["changed", "introduced"],
    );

    expect((await marketplace.readAll()).entries).toEqual({
      keep: "untouched",
      changed: "before",
    });
  });

  it("closes a late open connection after timeout without touching a canary", async () => {
    let request: {
      result: IDBDatabase | null;
      onsuccess: (() => void) | null;
      onerror: (() => void) | null;
      onblocked: (() => void) | null;
      onupgradeneeded: (() => void) | null;
    } | undefined;
    let closeCount = 0;
    const canary = { value: "untouched" };
    const factory: IDBFactory = {
      open: () => {
        request = {
          result: null,
          onsuccess: null,
          onerror: null,
          onblocked: null,
          onupgradeneeded: null,
        };
        return request as unknown as IDBOpenDBRequest;
      },
    } as unknown as IDBFactory;

    const store = indexedDbMarketplaceStore(factory, 10);
    await expect(store.readAll()).resolves.toMatchObject({
      available: false,
      entries: {},
    });
    await new Promise((resolve) => setTimeout(resolve, 15));

    const lateRequest = request;
    if (!lateRequest) throw new Error("The open request fixture was not created.");
    lateRequest.result = {
      objectStoreNames: { contains: () => true },
      close: () => {
        closeCount += 1;
      },
    } as unknown as IDBDatabase;
    lateRequest.onsuccess?.();

    expect(closeCount).toBe(1);
    expect(canary).toEqual({ value: "untouched" });
  });

  it("keeps a blocked delete pending, reuses it, and completes after the canary closes", async () => {
    let deleteCalls = 0;
    let request: {
      onsuccess: (() => void) | null;
      onerror: (() => void) | null;
      onblocked: (() => void) | null;
    } | undefined;
    const canary = { value: "untouched" };
    const factory: IDBFactory = {
      deleteDatabase: () => {
        deleteCalls += 1;
        const current: {
          onsuccess: (() => void) | null;
          onerror: (() => void) | null;
          onblocked: (() => void) | null;
        } = { onsuccess: null, onerror: null, onblocked: null };
        request = current;
        queueMicrotask(() => current.onblocked?.());
        return current as unknown as IDBOpenDBRequest;
      },
    } as unknown as IDBFactory;
    const store = indexedDbMarketplaceStore(factory, 100);
    const statuses: string[] = [];
    const unsubscribe = store.subscribeDeleteStatus((status) => {
      statuses.push(status.phase);
    });

    const first = store.deleteAll();
    const duplicate = store.deleteAll();
    expect(duplicate).toBe(first);
    await Promise.resolve();
    expect(store.getDeleteStatus().phase).toBe("pending");
    expect(deleteCalls).toBe(1);
    expect(canary).toEqual({ value: "untouched" });

    const blockedRequest = request;
    if (!blockedRequest) throw new Error("The delete request fixture was not created.");
    blockedRequest.onsuccess?.();
    await expect(first).resolves.toBeUndefined();
    expect(store.getDeleteStatus()).toEqual({
      phase: "succeeded",
      detail: "Marketplace storage was reset.",
    });
    expect(statuses).toEqual(["idle", "pending", "pending", "succeeded"]);
    expect(canary).toEqual({ value: "untouched" });
    unsubscribe();
  });

  it("marks a timed-out delete pending and resolves when it eventually succeeds", async () => {
    let request: {
      onsuccess: (() => void) | null;
      onerror: (() => void) | null;
      onblocked: (() => void) | null;
    } | undefined;
    const canary = { value: "untouched" };
    const succeeding: IDBFactory = {
      deleteDatabase: () => {
        const current: {
          onsuccess: (() => void) | null;
          onerror: (() => void) | null;
          onblocked: (() => void) | null;
        } = { onsuccess: null, onerror: null, onblocked: null };
        request = current;
        queueMicrotask(() => current.onblocked?.());
        return current as unknown as IDBOpenDBRequest;
      },
    } as unknown as IDBFactory;
    const store = indexedDbMarketplaceStore(succeeding, 10);
    let settled = false;
    const operation = store.deleteAll().then(() => {
      settled = true;
    });

    await new Promise((resolve) => setTimeout(resolve, 15));
    expect(store.getDeleteStatus().phase).toBe("timed-out");
    expect(settled).toBe(false);
    expect(canary).toEqual({ value: "untouched" });

    const timedOutRequest = request;
    if (!timedOutRequest) throw new Error("The delete request fixture was not created.");
    timedOutRequest.onsuccess?.();
    await operation;
    expect(settled).toBe(true);
    expect(store.getDeleteStatus().phase).toBe("succeeded");
    expect(canary).toEqual({ value: "untouched" });
  });

  it("reports a terminal delete error after a blocked wait", async () => {
    let request: {
      onsuccess: (() => void) | null;
      onerror: (() => void) | null;
      onblocked: (() => void) | null;
    } | undefined;
    const canary = { value: "untouched" };
    const failing: IDBFactory = {
      deleteDatabase: () => {
        const current: {
          onsuccess: (() => void) | null;
          onerror: (() => void) | null;
          onblocked: (() => void) | null;
        } = { onsuccess: null, onerror: null, onblocked: null };
        request = current;
        queueMicrotask(() => current.onblocked?.());
        return current as unknown as IDBOpenDBRequest;
      },
    } as unknown as IDBFactory;
    const store = indexedDbMarketplaceStore(failing, 100);
    const operation = store.deleteAll();
    await Promise.resolve();
    expect(store.getDeleteStatus().phase).toBe("pending");
    const failingRequest = request;
    if (!failingRequest) throw new Error("The delete request fixture was not created.");
    failingRequest.onerror?.();

    await expect(operation).rejects.toThrow(/could not be reset/);
    expect(store.getDeleteStatus().phase).toBe("failed");
    expect(canary).toEqual({ value: "untouched" });
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
    expect(() =>
      parseBackup(
        JSON.stringify({
          schemaVersion: 1.5,
          engine: {},
        }),
      ),
    ).toThrow(/supported integer/);
    expect(() =>
      parseBackup(
        JSON.stringify({
          schemaVersion: BACKUP_SCHEMA_VERSION,
          engine: stateFixture(new Date("2026-09-03T10:00:00.000Z")),
          marketplace: [],
        }),
      ),
    ).toThrow(/malformed Marketplace/);
  });

  it("accepts a raw profile as an engine-only restore", () => {
    const state = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    const restored = parseRestoreSource(serializeProfileForTest(state));

    expect(restored.engine).toEqual(state);
    expect(restored.marketplace).toEqual({});
    expect(restored.createdAt).toBeNull();
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

  it("compensates Marketplace when the engine store fails after its write", async () => {
    const storage = memoryStorage();
    const before = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    before.name = "Before";
    const beforeRaw = `${JSON.stringify(before, null, 2)}\n`;
    storage.set(ENGINE_STORAGE_KEY, beforeRaw);
    const engine = restoreEngineFixture(before, storage);
    engine.failTargetWrite = true;
    const marketplace = memoryMarketplace({
      "marketplace:active-tab": "Extensions",
      "keep-me": { version: 1 },
    });
    const restoredState = structuredClone(before);
    restoredState.name = "Restored";
    const source = serializeBackup(
      createBackup(
        restoredState,
        { "marketplace:active-tab": "Themes", introduced: true },
        new Date("2026-09-03T11:00:00.000Z"),
      ),
    );
    const retained: RecoveryRecord[] = [];

    await expect(
      restoreBackupTransaction(parseBackup(source), {
        engine,
        marketplaceStore: marketplace,
        retainRecovery: (record) => retained.push(structuredClone(record)),
      }),
    ).rejects.toThrow(/previous state was restored/);

    expect(retained).toHaveLength(1);
    expect((await marketplace.readAll()).entries).toEqual({
      "marketplace:active-tab": "Extensions",
      "keep-me": { version: 1 },
    });
    expect(new EngineStore(storage).load()).toEqual(before);
    expect(storage.get(ENGINE_STORAGE_KEY)).toBe(beforeRaw);
    expect(engine.state).toEqual(before);
  });

  it("retains a recovery record naming Marketplace when compensation fails", async () => {
    const storage = memoryStorage();
    const before = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    before.name = "Before";
    storage.set(ENGINE_STORAGE_KEY, JSON.stringify(before));
    const engine = restoreEngineFixture(before, storage);
    engine.failTargetWrite = true;
    let entries: MarketplaceEntries = { existing: "before" };
    const marketplace: MarketplaceStore = {
      readAll: () => Promise.resolve({ available: true, entries: { ...entries } }),
      writeAll: (next) => {
        entries = { ...entries, ...next };
        return Promise.resolve();
      },
      restoreKeys: () => Promise.reject(new Error("Marketplace compensation failed")),
      deleteAll: () => Promise.resolve(),
    };
    const restoredState = structuredClone(before);
    restoredState.name = "Restored";
    const source = serializeBackup(
      createBackup(restoredState, { introduced: true }, new Date("2026-09-03T11:00:00.000Z")),
    );

    const error = await restoreBackupTransaction(parseBackup(source), {
      engine,
      marketplaceStore: marketplace,
      retainRecovery: (record) => new EngineStore(storage).writeRecovery(record),
    }).catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(RestoreTransactionError);
    expect((error as RestoreTransactionError).incomplete).toEqual(["marketplace"]);
    expect((error as Error).message).toMatch(/Marketplace/);
    const recovery = new EngineStore(storage).readRecovery();
    expect(recovery?.kind).toBe("restore");
    expect(recovery?.incomplete).toEqual(["marketplace"]);
    expect(parseRestoreSource(recovery?.raw ?? "").engine).toEqual(before);
    expect(new EngineStore(storage).load()).toEqual(before);
    expect(entries).toEqual({ existing: "before", introduced: true });
  });

  it("keeps a bounded recovery record outside the engine and Marketplace stores", () => {
    const storage = memoryStorage();
    const before = stateFixture(new Date("2026-09-03T10:00:00.000Z"));
    storage.set(ENGINE_STORAGE_KEY, JSON.stringify(before));
    const raw = serializeBackup(
      createBackup(before, { "marketplace:active-tab": "Themes" }, new Date("2026-09-03T11:00:00.000Z")),
    );
    const record: RecoveryRecord = {
      schemaVersion: 1,
      kind: "marketplace-reset",
      createdAt: "2026-09-03T11:00:00.000Z",
      message: "Marketplace storage was reset.",
      incomplete: [],
      raw,
    };
    const store = new EngineStore(storage);

    store.writeRecovery(record);
    expect(new EngineStore(storage).readRecovery()).toEqual(record);
    expect(new EngineStore(storage).load()).toEqual(before);

    store.discardRecovery();
    expect(new EngineStore(storage).readRecovery()).toBeNull();
    expect(new EngineStore(storage).load()).toEqual(before);
  });

  it("refuses an oversized recovery record without replacing the retained copy", () => {
    const storage = memoryStorage();
    const store = new EngineStore(storage);
    const retained: RecoveryRecord = {
      schemaVersion: 1,
      kind: "marketplace-reset",
      createdAt: "2026-09-03T11:00:00.000Z",
      message: "Retained copy.",
      incomplete: [],
      raw: "{}",
    };
    store.writeRecovery(retained);

    expect(() =>
      store.writeRecovery({
        ...retained,
        raw: "x".repeat(MAX_RECOVERY_RECORD_BYTES),
      }),
    ).toThrow(/exceeds/);
    expect(new EngineStore(storage).readRecovery()).toEqual(retained);
  });
});

function serializeProfileForTest(state: ReturnType<typeof createDefaultState>): string {
  return JSON.stringify({
    schemaVersion: 1,
    settings: {
      LibreSpot_EngineProfileJson: JSON.stringify(state),
    },
  });
}
