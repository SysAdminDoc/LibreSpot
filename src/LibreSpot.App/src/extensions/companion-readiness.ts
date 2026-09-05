export type CompanionApiSurface = {
  React?: unknown;
  Platform?: { History?: unknown };
  LocalStorage?: unknown;
  Player?: unknown;
};

export function isCompanionApiReady(
  api: CompanionApiSurface | undefined,
): boolean {
  return Boolean(
    api?.React &&
    api.Platform?.History &&
    api.LocalStorage &&
    api.Player,
  );
}
