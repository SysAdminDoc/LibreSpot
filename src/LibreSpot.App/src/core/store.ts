import {
  createDefaultState,
  PROFILE_SCHEMA_VERSION,
  type EngineState,
} from "./state.ts";
import { parseProfile, serializeEngineState } from "./profile.ts";

export const ENGINE_STORAGE_KEY = "librespot:engine-state";

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
    } catch {
      this.storage.remove(ENGINE_STORAGE_KEY);
      return createDefaultState(this.now());
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
