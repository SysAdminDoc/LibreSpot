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
  /**
   * The unreadable value from this session's load when it could not be copied
   * anywhere. Keeping it in memory is what stops a full profile turning into a
   * silent loss: storage refused the copy, so nothing on disk points at it, and
   * the next save would overwrite the original with no one ever told it existed.
   */
  private unrecovered: QuarantinedState | null = null;

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
      // and let Health hand them back. If the copy could not be written, for
      // instance because the profile's storage is full, the original stays
      // exactly where it is: an unreadable state the user still has beats a
      // readable default they cannot undo.
      if (this.quarantine(raw, error)) {
        this.storage.remove(ENGINE_STORAGE_KEY);
      }
      return createDefaultState(this.now());
    }
  }

  /** The unreadable state kept from the last failed load, if there is one. */
  public readQuarantine(): QuarantinedState | null {
    // The in-memory copy wins: it exists only when storage refused to hold one,
    // and it is the sole remaining route to those bytes.
    if (this.unrecovered !== null) {
      return this.unrecovered;
    }
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
    this.unrecovered = null;
    const key = this.storage.get(QUARANTINE_POINTER_KEY);
    // Only ever remove a key this class owns. A pointer left by some other
    // scheme could otherwise name the live state and Discard would delete it.
    if (key?.startsWith(QUARANTINE_KEY_PREFIX) === true) {
      this.storage.remove(key);
    }
    this.storage.remove(QUARANTINE_POINTER_KEY);
  }

  /** True when the raw value is safely stored and the original can be dropped. */
  private quarantine(raw: string, error: unknown): boolean {
    const candidate: QuarantinedState = {
      key: "",
      quarantinedAt: this.now().toISOString(),
      reason:
        error instanceof Error
          ? error.message
          : "The saved state could not be read.",
      raw,
    };

    if (this.writeQuarantine(candidate)) {
      this.unrecovered = null;
      return true;
    }

    // Storage refused the copy. Hold it for this session so Health can still
    // offer Export; the original stays on disk untouched behind it.
    this.unrecovered = candidate;
    return false;
  }

  /** Writes one quarantine record and its pointer. False when storage refuses. */
  private writeQuarantine(record: QuarantinedState): boolean {
    const key = `${QUARANTINE_KEY_PREFIX}${record.quarantinedAt}`;
    const previous = (() => {
      try {
        return this.storage.get(QUARANTINE_POINTER_KEY);
      } catch {
        return null;
      }
    })();

    try {
      this.storage.set(
        key,
        JSON.stringify({
          quarantinedAt: record.quarantinedAt,
          reason: record.reason,
          raw: record.raw,
        }),
      );
      this.storage.set(QUARANTINE_POINTER_KEY, key);
    } catch {
      return false;
    }

    // Only once the new copy is readable does the older one go. Removing it
    // first would leave nothing at all if the write above failed.
    if (previous !== null && previous !== key && previous.startsWith(QUARANTINE_KEY_PREFIX)) {
      try {
        this.storage.remove(previous);
      } catch {
        // An orphan costs space, not data.
      }
    }

    return true;
  }

  public save(state: EngineState): EngineState {
    // A save is what would overwrite an unreadable original that storage
    // refused to copy. Try the copy once more first, so the recovery survives
    // if whatever filled the profile has since been freed.
    if (this.unrecovered !== null && this.writeQuarantine(this.unrecovered)) {
      this.unrecovered = null;
    }

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
