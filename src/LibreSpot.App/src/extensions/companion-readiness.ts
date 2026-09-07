export type CompanionApiSurface = {
  React?: unknown;
  Platform?: { History?: unknown };
  LocalStorage?: unknown;
  Player?: unknown;
};

type ObjectSurface = Record<string, unknown>;

function objectSurface(value: unknown): ObjectSurface | null {
  return typeof value === "object" && value !== null
    ? (value as ObjectSurface)
    : null;
}

function isCallable(value: unknown): boolean {
  return typeof value === "function";
}

export function isCompanionApiReady(
  api: CompanionApiSurface | undefined,
): boolean {
  const react = objectSurface(api?.React);
  const history = objectSurface(api?.Platform?.History);
  const location = objectSurface(history?.location);
  const localStorage = objectSurface(api?.LocalStorage);
  const player = objectSurface(api?.Player);

  return Boolean(
    react?.Fragment &&
    isCallable(react.createElement) &&
    isCallable(react.useCallback) &&
    isCallable(react.useEffect) &&
    isCallable(react.useMemo) &&
    isCallable(react.useState) &&
    typeof location?.pathname === "string" &&
        isCallable(history?.push) &&
    isCallable(localStorage?.get) &&
    isCallable(localStorage?.set) &&
    isCallable(player?.addEventListener),
  );
}
