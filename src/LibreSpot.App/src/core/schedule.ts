import type { EngineState } from "./state.ts";

function minuteOfDay(value: string): number {
  const match = /^(\d{2}):(\d{2})$/.exec(value);
  if (!match) {
    throw new Error(`Invalid clock value "${value}".`);
  }
  const hour = Number(match[1]);
  const minute = Number(match[2]);
  if (hour > 23 || minute > 59) {
    throw new Error(`Invalid clock value "${value}".`);
  }
  return hour * 60 + minute;
}

export function isLightScheduleActive(
  now: Date,
  lightStart: string,
  darkStart: string,
): boolean {
  const current = now.getHours() * 60 + now.getMinutes();
  const light = minuteOfDay(lightStart);
  const dark = minuteOfDay(darkStart);
  if (light === dark) {
    return false;
  }
  return light < dark
    ? current >= light && current < dark
    : current >= light || current < dark;
}

export function resolveScheduledScheme(
  state: EngineState,
  now = new Date(),
): string {
  if (!state.schedule.enabled) {
    return state.scheme;
  }
  return isLightScheduleActive(
    now,
    state.schedule.lightStart,
    state.schedule.darkStart,
  )
    ? state.schedule.lightScheme
    : state.schedule.darkScheme;
}
