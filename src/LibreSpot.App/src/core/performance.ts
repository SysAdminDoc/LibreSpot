import type { EffectsTier } from "./state.ts";

export type FrameClock = {
  now(): number;
  requestFrame(callback: () => void): number;
  cancelFrame(handle: number): void;
};

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
  };
}

export async function probeFrameRate(
  clock: FrameClock,
  durationMs = 1000,
): Promise<number> {
  if (durationMs <= 0) {
    throw new Error("Frame probe duration must be positive.");
  }
  return await new Promise<number>((resolve) => {
    const started = clock.now();
    let frames = 0;
    let handle = 0;
    const count = () => {
      frames += 1;
      const elapsed = clock.now() - started;
      if (elapsed >= durationMs) {
        clock.cancelFrame(handle);
        resolve(Math.round((frames * 1000) / elapsed));
        return;
      }
      handle = clock.requestFrame(count);
    };
    handle = clock.requestFrame(count);
  });
}

export function prefersReducedMotion(window: Window): boolean {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}
