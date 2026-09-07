import {
  browserFrameClock,
  type FrameClock,
  probeFrameRate,
} from "../src/core/performance.ts";

describe("frame performance probing", () => {
  it("defers measurement for a background window", async () => {
    let requests = 0;
    const clock: FrameClock = {
      now: () => 0,
      requestFrame: () => {
        requests += 1;
        return 1;
      },
      cancelFrame: () => undefined,
      isBackground: () => true,
    };

    await expect(probeFrameRate(clock, 20, 40)).resolves.toBeNull();
    expect(requests).toBe(0);
  });

  it("finishes with no result when frames never arrive", async () => {
    vi.useFakeTimers();
    try {
      const clock: FrameClock = {
        now: () => 0,
        requestFrame: () => 1,
        cancelFrame: () => undefined,
      };
      const result = probeFrameRate(clock, 20, 40);
      await vi.advanceTimersByTimeAsync(40);
      await expect(result).resolves.toBeNull();
    } finally {
      vi.useRealTimers();
    }
  });

  it("measures an active frame stream", async () => {
    let now = 0;
    let nextHandle = 0;
    const timers = new Map<number, ReturnType<typeof setTimeout>>();
    const clock: FrameClock = {
      now: () => now,
      requestFrame: (callback) => {
        const handle = ++nextHandle;
        timers.set(
          handle,
          setTimeout(() => {
            timers.delete(handle);
            now += 16;
            callback();
          }, 0),
        );
        return handle;
      },
      cancelFrame: (handle) => {
        const timer = timers.get(handle);
        if (timer !== undefined) {
          clearTimeout(timer);
          timers.delete(handle);
        }
      },
    };

    await expect(probeFrameRate(clock, 48, 500)).resolves.toBe(63);
  });

  it("marks browser clocks as background when the document is hidden", () => {
    const original = Object.getOwnPropertyDescriptor(document, "visibilityState");
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      value: "hidden",
    });
    try {
      expect(browserFrameClock(window).isBackground?.()).toBe(true);
    } finally {
      if (original) {
        Object.defineProperty(document, "visibilityState", original);
      } else {
        Reflect.deleteProperty(document, "visibilityState");
      }
    }
  });
});
