import type { EffectsTier } from "./state.ts";

export type FrameClock = {
  now(): number;
  requestFrame(callback: () => void): number;
  cancelFrame(handle: number): void;
  /** Returns true when the host cannot provide a representative foreground sample. */
  isBackground?(): boolean;
  setTimeout?(callback: () => void, delayMs: number): number;
  clearTimeout?(handle: number): void;
};

export const DEFAULT_FRAME_PROBE_DURATION_MS = 1000;
export const DEFAULT_FRAME_PROBE_TIMEOUT_MS = 1500;

export function classifyFrameRate(fps: number): EffectsTier {
  if (!Number.isFinite(fps) || fps <= 0) {
    return "flat";
  }
  if (fps < 30) {
    return "flat";
  }
  if (fps < 48) {
    return "eco";
  }
  return "glass";
}

export function browserFrameClock(window: Window): FrameClock {
  return {
    now: () => window.performance.now(),
    requestFrame: (callback) =>
      window.requestAnimationFrame(() => {
        callback();
      }),
    cancelFrame: (handle) => {
      window.cancelAnimationFrame(handle);
    },
    isBackground: () =>
      window.document.visibilityState === "hidden" || window.document.hidden,
    setTimeout: (callback, delayMs) => window.setTimeout(callback, delayMs),
    clearTimeout: (handle) => window.clearTimeout(handle),
  };
}

export async function probeFrameRate(
  clock: FrameClock,
  durationMs = DEFAULT_FRAME_PROBE_DURATION_MS,
  timeoutMs = Math.max(DEFAULT_FRAME_PROBE_TIMEOUT_MS, durationMs + 500),
): Promise<number | null> {
  if (durationMs <= 0) {
    throw new Error("Frame probe duration must be positive.");
  }
  if (timeoutMs <= 0) {
    throw new Error("Frame probe timeout must be positive.");
  }
  if (clock.isBackground?.()) {
    return null;
  }
  return await new Promise<number | null>((resolve) => {
    const started = clock.now();
    let frames = 0;
    let frameHandle: number | undefined;
    const timeout = { handle: undefined as number | undefined };
    let settled = false;

    const clearScheduledTimeout = (handle: number): void => {
      if (clock.clearTimeout) {
        clock.clearTimeout(handle);
        return;
      }
      globalThis.clearTimeout(handle as unknown as ReturnType<typeof setTimeout>);
    };

    const finish = (fps: number | null): void => {
      if (settled) {
        return;
      }
      settled = true;
      if (frameHandle !== undefined) {
        clock.cancelFrame(frameHandle);
      }
      if (timeout.handle !== undefined) {
        clearScheduledTimeout(timeout.handle);
      }
      resolve(fps);
    };

    const trackFrame = (handle: number): void => {
      if (settled) {
        clock.cancelFrame(handle);
      } else {
        frameHandle = handle;
      }
    };

    const count = () => {
      frameHandle = undefined;
      if (clock.isBackground?.()) {
        finish(null);
        return;
      }
      frames += 1;
      const elapsed = clock.now() - started;
      if (elapsed >= durationMs) {
        finish(elapsed > 0 ? Math.round((frames * 1000) / elapsed) : null);
        return;
      }
      trackFrame(clock.requestFrame(count));
    };

    const scheduleTimeout = clock.setTimeout
      ? clock.setTimeout.bind(clock)
      : (callback: () => void, delayMs: number) =>
          globalThis.setTimeout(callback, delayMs) as unknown as number;
    timeout.handle = scheduleTimeout(() => finish(null), timeoutMs);
    trackFrame(clock.requestFrame(count));
  });
}

export function prefersReducedMotion(window: Window): boolean {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}
