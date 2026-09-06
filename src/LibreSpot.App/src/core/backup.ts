import { PROFILE_SCHEMA_VERSION, type EngineState } from "./state.ts";
import {
  ENGINE_VERSION,
  MAX_PROFILE_BYTES,
  parseProfile,
  serializeProfile,
} from "./profile.ts";
import {
  RECOVERY_RECORD_SCHEMA_VERSION,
  type RecoveryRecord,
} from "./store.ts";

/**
 * One file that holds everything a person would lose if their Spotify profile
 * were cleared: the LibreSpot engine state and the settings Marketplace keeps in
 * its own database. Both stay on the machine; this is a file, not a sync.
 */
export const BACKUP_SCHEMA_VERSION = 1;
export const MAX_BACKUP_BYTES = 8 * 1024 * 1024;
export const MAX_MARKETPLACE_BYTES = 2 * 1024 * 1024;

export const MARKETPLACE_DATABASE = "spicetify-marketplace";
export const MARKETPLACE_STORE = "settings";

export type MarketplaceEntries = Record<string, unknown>;

export type LibreSpotBackup = {
  schemaVersion: number;
  generator: string;
  generatorVersion: string;
  createdAt: string;
  engine: EngineState;
  marketplace: MarketplaceEntries;
  /** The desktop reads this to import the backup as a profile. */
  profile: unknown;
};

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function createBackup(
  state: EngineState,
  marketplace: MarketplaceEntries,
  createdAt: Date,
): LibreSpotBackup {
  return {
    schemaVersion: BACKUP_SCHEMA_VERSION,
    generator: "LibreSpot-Spotify",
    generatorVersion: ENGINE_VERSION,
    createdAt: createdAt.toISOString(),
    engine: structuredClone(state),
    marketplace: structuredClone(marketplace),
    // The same envelope LibreSpot Desktop already imports, so one file works in
    // both places instead of two exports that can drift apart.
    profile: JSON.parse(serializeProfile(state)),
  };
}

export function serializeBackup(backup: LibreSpotBackup): string {
  return `${JSON.stringify(backup, null, 2)}\n`;
}

export type ParsedBackup = {
  engine: EngineState;
  marketplace: MarketplaceEntries;
  createdAt: string | null;
};

export function parseBackup(source: string): ParsedBackup {
  if (new TextEncoder().encode(source).length > MAX_BACKUP_BYTES) {
    throw new Error(`LibreSpot backup exceeds the ${MAX_BACKUP_BYTES}-byte limit.`);
  }
  const parsed: unknown = JSON.parse(source);
  if (!isRecord(parsed)) {
    throw new Error("A LibreSpot backup must be a JSON object.");
  }

  const schemaVersion = parsed.schemaVersion;
  if (typeof schemaVersion !== "number" || !Number.isInteger(schemaVersion) || schemaVersion < 1) {
    throw new Error("This file is not a LibreSpot backup: schemaVersion must be a supported integer.");
  }
  if (schemaVersion > BACKUP_SCHEMA_VERSION) {
    throw new Error(
      `This backup was written by a newer LibreSpot (schema ${schemaVersion}). Update LibreSpot and try again.`,
    );
  }

  if (!isRecord(parsed.engine)) {
    throw new Error("This backup has no engine state to restore.");
  }
  if (parsed.engine.schemaVersion !== PROFILE_SCHEMA_VERSION) {
    throw new Error(
      `This backup holds a profile of schema ${String(parsed.engine.schemaVersion)}; this LibreSpot reads ${PROFILE_SCHEMA_VERSION}.`,
    );
  }

  // Reuse the profile reader so a backup is validated exactly like a profile is.
  const engine = parseProfile(JSON.stringify(parsed.engine));

  // A null-prototype object so a "__proto__" key is stored as data rather than
  // being swallowed by the prototype setter and lost from the restore.
  const marketplace = Object.create(null) as MarketplaceEntries;
  if ("marketplace" in parsed && !isRecord(parsed.marketplace)) {
    throw new Error("This backup has a malformed Marketplace section.");
  }
  if (isRecord(parsed.marketplace)) {
    if (new TextEncoder().encode(JSON.stringify(parsed.marketplace)).length > MAX_MARKETPLACE_BYTES) {
      throw new Error(
        `Marketplace settings exceed the ${MAX_MARKETPLACE_BYTES}-byte backup limit.`,
      );
    }
    for (const [key, value] of Object.entries(parsed.marketplace)) {
      Object.defineProperty(marketplace, key, {
        value,
        writable: true,
        enumerable: true,
        configurable: true,
      });
    }
  }

  return {
    engine,
    marketplace,
    createdAt: typeof parsed.createdAt === "string" ? parsed.createdAt : null,
  };
}

export function parseRestoreSource(source: string): ParsedBackup {
  const sourceBytes = new TextEncoder().encode(source).length;
  if (sourceBytes > MAX_BACKUP_BYTES) {
    throw new Error(`LibreSpot restore exceeds the ${MAX_BACKUP_BYTES}-byte limit.`);
  }
  // A raw profile must reach its bounded parser before the backup envelope is
  // parsed. Backups carry an engine object, so larger envelopes use the backup
  // limit while an oversized raw profile is rejected immediately.
  if (sourceBytes > MAX_PROFILE_BYTES && !/"engine"\s*:/.test(source)) {
    throw new Error(`LibreSpot profile exceeds the ${MAX_PROFILE_BYTES}-byte limit.`);
  }
  try {
    return parseBackup(source);
  } catch (backupError) {
    try {
      return {
        engine: parseProfile(source),
        marketplace: Object.create(null) as MarketplaceEntries,
        createdAt: null,
      };
    } catch {
      throw backupError;
    }
  }
}

/**
 * Minimal surface of the Marketplace database, so tests can supply a fake.
 * readAll reports whether it could read at all: an unreadable database and an
 * empty one both yield no entries, and a backup must never present the first as
 * the second.
 */
export type MarketplaceReadResult = {
  available: boolean;
  entries: MarketplaceEntries;
};

export type MarketplaceStore = {
  readAll(createIfMissing?: boolean): Promise<MarketplaceReadResult>;
  /**
   * Merges the supplied keys into Marketplace's settings store. Existing keys
   * outside the supplied set are intentionally left untouched.
   */
  writeAll(entries: MarketplaceEntries): Promise<void>;
  /**
   * Restores exactly the listed keys while leaving all other Marketplace keys
   * alone. Missing keys in the snapshot are removed.
   */
  restoreKeys(
    entries: MarketplaceEntries,
    keys: readonly string[],
  ): Promise<void>;
  /**
   * Removes Marketplace's whole database. Stale records from an older
   * install survive a full Spicetify reinstall and can put back themes the
   * user removed, which upstream closed as not planned.
   */
  deleteAll(): Promise<void>;
};

/**
 * Reads and writes Marketplace's own Dexie database directly. Marketplace has
 * used a `settings` object store keyed by string since 1.0.9, and its own backup
 * modal reads the same keys.
 */
export function indexedDbMarketplaceStore(
  factory: IDBFactory,
  timeoutMs = 8000,
): MarketplaceStore {
  const open = (createIfMissing = false) =>
    new Promise<IDBDatabase | null>((resolve) => {
      let settled = false;
      const finish = (database: IDBDatabase | null) => {
        if (settled) return;
        settled = true;
        resolve(database);
      };

      // An open request can sit forever when another connection blocks a version
      // change, so every path out of here is bounded.
      const timer = setTimeout(() => {
        finish(null);
      }, timeoutMs);
      const settle = (database: IDBDatabase | null) => {
        clearTimeout(timer);
        finish(database);
      };

      let request: IDBOpenDBRequest;
      try {
        request = factory.open(MARKETPLACE_DATABASE);
      } catch {
        settle(null);
        return;
      }
      request.onerror = () => {
        settle(null);
      };
      request.onblocked = () => {
        settle(null);
      };
      request.onupgradeneeded = () => {
        const database = request.result;
        if (database.objectStoreNames.contains(MARKETPLACE_STORE)) {
          return;
        }
        if (!createIfMissing) {
          // Marketplace owns this database. If it does not exist yet there is
          // nothing to read, and normal backup operations must not invent its
          // schema.
          request.transaction?.abort();
          settle(null);
          return;
        }
        try {
          // A retained reset recovery is the one explicit path allowed to
          // recreate the known Marketplace schema before restoring its keys.
          database.createObjectStore(MARKETPLACE_STORE, { keyPath: "key" });
        } catch {
          request.transaction?.abort();
          settle(null);
        }
      };
      request.onsuccess = () => {
        const database = request.result;
        if (!database.objectStoreNames.contains(MARKETPLACE_STORE)) {
          database.close();
          settle(null);
          return;
        }
        settle(database);
      };
    });

  return {
    readAll: async (createIfMissing = false) => {
      const database = await open(createIfMissing);
      if (!database) return { available: false, entries: {} };
      try {
        return await new Promise<MarketplaceReadResult>((resolve) => {
          let request: IDBRequest<unknown[]>;
          try {
            const transaction = database.transaction(MARKETPLACE_STORE, "readonly");
            request = transaction.objectStore(MARKETPLACE_STORE).getAll();
            transaction.oncomplete = () => {
              const entries: MarketplaceEntries = Object.create(null) as MarketplaceEntries;
              for (const record of request.result) {
                // Marketplace stores { key, value } records with an in-line key.
                if (isRecord(record) && typeof record.key === "string") {
                  entries[record.key] = record.value;
                }
              }
              resolve({ available: true, entries });
            };
            transaction.onerror = () => {
              resolve({ available: false, entries: {} });
            };
            transaction.onabort = () => {
              resolve({ available: false, entries: {} });
            };
          } catch {
            resolve({ available: false, entries: {} });
          }
        });
      } finally {
        database.close();
      }
    },
    writeAll: async (entries) => {
      const database = await open();
      if (!database) {
        throw new Error(
          "Marketplace's database is not available, so its settings were not restored. Open Marketplace once and try again.",
        );
      }
      try {
        await new Promise<void>((resolve, reject) => {
          // Anything thrown while queueing has to reject: a transaction whose
          // body throws never fires oncomplete or onerror, which would leave
          // this promise pending forever.
          try {
            const transaction = database.transaction(MARKETPLACE_STORE, "readwrite");
            const store = transaction.objectStore(MARKETPLACE_STORE);
            for (const [key, value] of Object.entries(entries)) {
              // In-line key: the record carries its own key, and passing a second
              // argument to put() is a DataError.
              store.put({ key, value });
            }
            transaction.oncomplete = () => {
              resolve();
            };
            transaction.onerror = () => {
              reject(new Error("Marketplace's settings could not be written."));
            };
            transaction.onabort = () => {
              reject(new Error("Marketplace's settings could not be written."));
            };
          } catch (error) {
            reject(
              error instanceof Error
                ? error
                : new Error("Marketplace's settings could not be written."),
            );
          }
        });
      } finally {
        database.close();
      }
    },
    restoreKeys: async (entries, keys) => {
      const distinctKeys = [...new Set(keys)];
      if (distinctKeys.length === 0) {
        return;
      }

      const database = await open();
      if (!database) {
        throw new Error(
          "Marketplace's database is not available, so its previous settings could not be restored.",
        );
      }
      try {
        await new Promise<void>((resolve, reject) => {
          try {
            const transaction = database.transaction(MARKETPLACE_STORE, "readwrite");
            const store = transaction.objectStore(MARKETPLACE_STORE);
            for (const key of distinctKeys) {
              if (Object.prototype.hasOwnProperty.call(entries, key)) {
                store.put({ key, value: entries[key] });
              } else {
                store.delete(key);
              }
            }
            transaction.oncomplete = () => {
              resolve();
            };
            transaction.onerror = () => {
              reject(new Error("Marketplace's previous settings could not be restored."));
            };
            transaction.onabort = () => {
              reject(new Error("Marketplace's previous settings could not be restored."));
            };
          } catch (error) {
            reject(
              error instanceof Error
                ? error
                : new Error("Marketplace's previous settings could not be restored."),
            );
          }
        });
      } finally {
        database.close();
      }
    },
    deleteAll: () =>
      new Promise<void>((resolve, reject) => {
        // Bounded like open(): a delete blocks while any other connection
        // holds the database, and an unbounded wait would hang the button.
        let settled = false;
        const finish = (error?: Error) => {
          if (settled) return;
          settled = true;
          clearTimeout(timer);
          if (error) reject(error);
          else resolve();
        };
        const timer = setTimeout(() => {
          finish(
            new Error(
              "Marketplace's database is still open somewhere, so it was not reset. Close other Spotify windows and try again.",
            ),
          );
        }, timeoutMs);

        let request: IDBOpenDBRequest;
        try {
          request = factory.deleteDatabase(MARKETPLACE_DATABASE);
        } catch (error) {
          finish(
            error instanceof Error
              ? error
              : new Error("Marketplace's database could not be reset."),
          );
          return;
        }

        request.onsuccess = () => {
          finish();
        };
        request.onerror = () => {
          finish(new Error("Marketplace's database could not be reset."));
        };
        request.onblocked = () => {
          finish(
            new Error(
              "Marketplace's database is open in another Spotify window, so it was not reset.",
            ),
          );
        };
      }),
  };
}

export type RestoreEnginePort = {
  readonly state: EngineState;
  replace(state: EngineState): EngineState;
  readPersistedRaw?(): string | null;
  restoreExact?(state: EngineState, persistedRaw?: string | null): EngineState;
  refreshAccent(): Promise<unknown>;
  applyFlags(
    previousOverrides: Readonly<Record<string, boolean | number | string>>,
  ): Promise<unknown>;
};

export type RestoreTransactionResult = {
  state: EngineState;
  marketplaceCount: number;
  flagsChanged: boolean;
  flagResult: unknown;
};

export type RestoreHalf = "engine" | "marketplace";

export class RestoreTransactionError extends Error {
  public constructor(
    message: string,
    public readonly incomplete: readonly RestoreHalf[],
    public readonly recovery: RecoveryRecord,
    public readonly recoveryRetained: boolean,
    cause?: unknown,
  ) {
    super(message, cause === undefined ? undefined : { cause });
    this.name = "RestoreTransactionError";
  }
}

export type RestoreTransactionOptions = {
  engine: RestoreEnginePort;
  marketplaceStore: MarketplaceStore;
  /** Allows explicit reset recovery to recreate a missing Marketplace schema. */
  createMarketplaceIfMissing?: boolean;
  now?: () => Date;
  /** Called before compensation so a process exit cannot lose the snapshot. */
  retainRecovery?: (record: RecoveryRecord) => void;
  /** Called only after both stores have reached the requested state. */
  clearRecovery?: () => void;
};

function errorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

function recoveryHalfNames(halves: readonly RestoreHalf[]): string {
  return halves
    .map((half) => (half === "engine" ? "engine state" : "Marketplace settings"))
    .join(" and ");
}

function restoreEngineExact(
  engine: RestoreEnginePort,
  state: EngineState,
  persistedRaw?: string | null,
): EngineState {
  return engine.restoreExact?.(state, persistedRaw) ?? engine.replace(state);
}

/**
 * Restores a parsed backup as one recoverable operation. Marketplace's
 * writeAll intentionally keeps its merge behavior for the requested restore;
 * compensation uses restoreKeys so keys introduced by a failed write are
 * removed while unrelated keys remain untouched.
 */
export async function restoreBackupTransaction(
  restored: ParsedBackup,
  options: RestoreTransactionOptions,
): Promise<RestoreTransactionResult> {
  const now = options.now ?? (() => new Date());
  const beforeEngine = structuredClone(options.engine.state);
  const beforeEngineRaw = options.engine.readPersistedRaw?.();
  const beforeFlags = structuredClone(beforeEngine.featureOverrides);
  const targetKeys = Object.keys(restored.marketplace);
  let beforeMarketplace = Object.create(null) as MarketplaceEntries;
  let marketplaceRead = false;

  // Read the Marketplace snapshot before either store is touched. An
  // unavailable database is different from an empty one and must stop here.
  if (targetKeys.length > 0) {
    const current = await options.marketplaceStore.readAll(
      options.createMarketplaceIfMissing === true,
    );
    if (!current.available) {
      throw new Error(
        "Marketplace's settings could not be read, so restore stopped before changing anything. Close any other Spotify window and try again.",
      );
    }
    beforeMarketplace = structuredClone(current.entries);
    marketplaceRead = true;
  }

  const affectedKeys = [
    ...new Set([...Object.keys(beforeMarketplace), ...targetKeys]),
  ];
  let marketplaceAttempted = false;
  let engineAttempted = false;
  let flagsAttempted = false;

  try {
    if (targetKeys.length > 0) {
      // This is deliberately a merge. Marketplace owns keys we do not know
      // about, so a normal restore never deletes unrelated settings.
      marketplaceAttempted = true;
      await options.marketplaceStore.writeAll(restored.marketplace);
    }

    engineAttempted = true;
    const next = options.engine.replace(restored.engine);
    await options.engine.refreshAccent();
    const flagsChanged =
      JSON.stringify(next.featureOverrides) !== JSON.stringify(beforeFlags);
    let flagResult: unknown;
    if (flagsChanged) {
      flagsAttempted = true;
      flagResult = await options.engine.applyFlags(beforeFlags);
    }

    try {
      options.clearRecovery?.();
    } catch {
      // A stale recovery copy costs space but cannot make an already committed
      // restore unsafe. Health can dismiss it explicitly on a later run.
    }
    return {
      state: next,
      marketplaceCount: targetKeys.length,
      flagsChanged,
      flagResult,
    };
  } catch (error) {
    const attempted: RestoreHalf[] = [
      ...(engineAttempted ? (["engine"] as const) : []),
      ...(marketplaceAttempted ? (["marketplace"] as const) : []),
    ];
    const recovery: RecoveryRecord = {
      schemaVersion: RECOVERY_RECORD_SCHEMA_VERSION,
      kind: "restore",
      createdAt: now().toISOString(),
      message: "Restore compensation is pending.",
      incomplete: attempted,
      raw: serializeBackup(createBackup(beforeEngine, beforeMarketplace, now())),
    };
    let recoveryRetained = false;
    if (attempted.length > 0 && options.retainRecovery) {
      try {
        options.retainRecovery(recovery);
        recoveryRetained = true;
      } catch {
        // Compensation still runs. The final error names that no durable copy
        // was retained, which is safer than hiding the original failure.
      }
    }

    const incomplete: RestoreHalf[] = [];
    if (engineAttempted) {
      try {
        restoreEngineExact(options.engine, beforeEngine, beforeEngineRaw);
        if (flagsAttempted) {
          await options.engine.applyFlags(restored.engine.featureOverrides);
        }
        await options.engine.refreshAccent();
      } catch {
        incomplete.push("engine");
      }
    }
    if (marketplaceAttempted && marketplaceRead) {
      try {
        await options.marketplaceStore.restoreKeys(beforeMarketplace, affectedKeys);
      } catch {
        incomplete.push("marketplace");
      }
    }

    if (incomplete.length === 0) {
      try {
        options.clearRecovery?.();
      } catch {
        // Keep the retained record if dismissal is temporarily unavailable.
      }
      throw new RestoreTransactionError(
        `Restore failed: ${errorMessage(error, "an unknown error occurred")}. The previous state was restored.`,
        [],
        recovery,
        recoveryRetained,
        error,
      );
    }

    recovery.incomplete = [...incomplete];
    recovery.message = `Restore is incomplete for ${recoveryHalfNames(incomplete)}.`;
    if (options.retainRecovery) {
      try {
        options.retainRecovery(recovery);
        recoveryRetained = true;
      } catch {
        // If the first write succeeded, its snapshot is still present even if
        // storage refuses this final status update.
      }
    }
    const retainedMessage = recoveryRetained
      ? " A recovery copy was retained for Health."
      : " The recovery copy could not be retained.";
    throw new RestoreTransactionError(
      `Restore failed: ${errorMessage(error, "an unknown error occurred")}. Recovery is incomplete for ${recoveryHalfNames(incomplete)}.${retainedMessage}`,
      incomplete,
      recovery,
      recoveryRetained,
      error,
    );
  }
}
