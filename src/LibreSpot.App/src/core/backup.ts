import { PROFILE_SCHEMA_VERSION, type EngineState } from "./state.ts";
import { ENGINE_VERSION, parseProfile, serializeProfile } from "./profile.ts";

/**
 * One file that holds everything a person would lose if their Spotify profile
 * were cleared: the LibreSpot engine state and the settings Marketplace keeps in
 * its own database. Both stay on the machine; this is a file, not a sync.
 */
export const BACKUP_SCHEMA_VERSION = 1;

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
  const parsed: unknown = JSON.parse(source);
  if (!isRecord(parsed)) {
    throw new Error("A LibreSpot backup must be a JSON object.");
  }

  if (typeof parsed.schemaVersion !== "number") {
    throw new Error("This file is not a LibreSpot backup: it has no schemaVersion.");
  }
  if (parsed.schemaVersion > BACKUP_SCHEMA_VERSION) {
    throw new Error(
      `This backup was written by a newer LibreSpot (schema ${parsed.schemaVersion}). Update LibreSpot and try again.`,
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
  if (isRecord(parsed.marketplace)) {
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
  readAll(): Promise<MarketplaceReadResult>;
  writeAll(entries: MarketplaceEntries): Promise<void>;
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
  const open = () =>
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
        // Marketplace owns this database. If it does not exist yet there is
        // nothing to read, and LibreSpot must not invent its schema.
        request.transaction?.abort();
        settle(null);
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
    readAll: async () => {
      const database = await open();
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
  };
}
