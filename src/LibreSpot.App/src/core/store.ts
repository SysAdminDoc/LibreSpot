import {
  createDefaultState,
  PROFILE_SCHEMA_VERSION,
  type EngineState,
} from "./state.ts";
import { parseProfile, serializeEngineState } from "./profile.ts";

export const ENGINE_STORAGE_KEY = "librespot:engine-state";

/**
 * Where an unreadable saved state goes instead of the bin. The raw bytes land
 * under a dated key and the pointer names the one being kept, so recovery needs
 * no way to enumerate storage: Spicetify's LocalStorage API cannot list keys.
 */
export const QUARANTINE_POINTER_KEY = "librespot:engine-state:quarantine";
export const QUARANTINE_KEY_PREFIX = "librespot:engine-state:quarantine:";

export type QuarantinedState = {
  key: string;
  quarantinedAt: string;
  reason: string;
  raw: string;
};

export type StorageAdapter = {
  get(key: string): string | null;
  set(key: string, value: string): void;
  remove(key: string): void;
};

export function browserStorage(storage: Storage): StorageAdapter {
  return {
    get: (key) => storage.getItem(key),
    set: (key, value) => {
      storage.setItem(key, value);
    },
    remove: (key) => {
      storage.removeItem(key);
    },
  };
}

export class EngineStore {
  public constructor(
    private readonly storage: StorageAdapter,
    private readonly now: () => Date = () => new Date(),
  ) {}

  public load(): EngineState {
    const raw = this.storage.get(ENGINE_STORAGE_KEY);
    if (!raw) {
      return createDefaultState(this.now());
    }
    try {
      return parseProfile(raw);
    } catch (error) {
      // Deleting the only copy is how a truncated write or a schema bump turns
      // into "every theme, tweak and preset I saved is gone". Keep the bytes
      // and let Health hand them back.
      this.quarantine(raw, error);
      this.storage.remove(ENGINE_STORAGE_KEY);
      return createDefaultState(this.now());
    }
  }

  /** The unreadable state kept from the last failed load, if there is one. */
  public readQuarantine(): QuarantinedState | null {
    const key = this.storage.get(QUARANTINE_POINTER_KEY);
    if (key?.startsWith(QUARANTINE_KEY_PREFIX) !== true) {
      return null;
    }
    const stored = this.storage.get(key);
    if (stored === null) {
      return null;
    }
    let parsed: unknown;
    try {
      parsed = JSON.parse(stored);
    } catch {
      return null;
    }
    if (typeof parsed !== "object" || parsed === null || Array.isArray(parsed)) {
      return null;
    }
    const record = parsed as Record<string, unknown>;
    if (typeof record.raw !== "string") {
      return null;
    }
    return {
      key,
      quarantinedAt:
        typeof record.quarantinedAt === "string"
          ? record.quarantinedAt
          : key.slice(QUARANTINE_KEY_PREFIX.length),
      reason:
        typeof record.reason === "string"
          ? record.reason
          : "The saved state could not be read.",
      raw: record.raw,
    };
  }

  /** Drops the kept copy once the user has exported it or decided against it. */
  public discardQuarantine(): void {
    const key = this.storage.get(QUARANTINE_POINTER_KEY);
    if (key !== null) {
      this.storage.remove(key);
    }
    this.storage.remove(QUARANTINE_POINTER_KEY);
  }

  private quarantine(raw: string, error: unknown): void {
    const quarantinedAt = this.now().toISOString();
    const key = `${QUARANTINE_KEY_PREFIX}${quarantinedAt}`;
    try {
      // One retained copy. A profile that fails to parse on every load would
      // otherwise stack a new dated key each time and fill the storage quota.
      const previous = this.storage.get(QUARANTINE_POINTER_KEY);
      if (previous !== null && previous !== key) {
        this.storage.remove(previous);
      }
      this.storage.set(
        key,
        JSON.stringify({
          quarantinedAt,
          reason:
            error instanceof Error
              ? error.message
              : "The saved state could not be read.",
          raw,
        }),
      );
      this.storage.set(QUARANTINE_POINTER_KEY, key);
    } catch {
      // A full or unavailable store must not stop the engine from starting.
    }
  }

  public save(state: EngineState): EngineState {
    const next = structuredClone(state);
    next.schemaVersion = PROFILE_SCHEMA_VERSION;
    next.updatedAt = this.now().toISOString();
    this.storage.set(ENGINE_STORAGE_KEY, serializeEngineState(next));
    return next;
  }

  public reset(): EngineState {
    this.storage.remove(ENGINE_STORAGE_KEY);
    return createDefaultState(this.now());
  }
}
