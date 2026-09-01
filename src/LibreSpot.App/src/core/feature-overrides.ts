export type FeatureValue = boolean | number | string;

export type RemoteProperty = {
  source: string;
  type: string;
  name: string;
  localValue?: FeatureValue;
};

export type RemoteConfigDebugApi = {
  getProperties?: () => Promise<RemoteProperty[]>;
  setOverrides?: (
    entries: { ref: RemoteProperty; value: FeatureValue }[],
    options: { autoRunOverrideEffects: boolean },
  ) => Promise<unknown>;
  setOverride?: (...arguments_: unknown[]) => Promise<unknown>;
};

export type RemoteConfigResolver = {
  setOverrides?: (overrides: Map<string, FeatureValue>) => unknown;
};

export type FeatureOverrideRuntime = {
  debugApi?: RemoteConfigDebugApi;
  resolver?: RemoteConfigResolver;
};

function valueType(value: FeatureValue): string {
  if (typeof value === "boolean") {
    return "boolean";
  }
  if (typeof value === "number") {
    return "number";
  }
  return "enum";
}

export async function applyFeatureOverrides(
  overrides: Readonly<Record<string, FeatureValue>>,
  runtime: FeatureOverrideRuntime,
): Promise<"debug-api" | "resolver" | "unavailable"> {
  const entries = Object.entries(overrides);
  if (entries.length === 0) {
    return "unavailable";
  }

  const api = runtime.debugApi;
  if (api?.getProperties && (api.setOverrides ?? api.setOverride)) {
    const properties = await api.getProperties();
    const resolved = entries.flatMap(([name, value]) => {
      const ref = properties.find(
        (property) =>
          property.source === "web" &&
          property.name === name &&
          property.type === valueType(value),
      );
      return ref ? [{ ref, value }] : [];
    });
    if (resolved.length === 0) {
      return "unavailable";
    }
    if (api.setOverrides) {
      await api.setOverrides(resolved, { autoRunOverrideEffects: true });
      return "debug-api";
    }
    if (api.setOverride) {
      for (const override of resolved) {
        await api.setOverride(override, {
          autoRunOverrideEffects: override.ref.localValue !== override.value,
        });
      }
      return "debug-api";
    }
  }

  if (runtime.resolver?.setOverrides) {
    runtime.resolver.setOverrides(new Map(entries));
    return "resolver";
  }
  return "unavailable";
}

export type CapturedFeature = {
  name: string;
  description: string;
  type: "bool" | "enum" | "number" | "string";
  default: FeatureValue;
  values?: string[];
  minimum?: number;
  maximum?: number;
};

export class FeatureCapture {
  readonly #features = new Map<string, CapturedFeature>();

  public capture(feature: CapturedFeature): CapturedFeature {
    const rawValues: unknown = feature.values;
    const values = Array.isArray(rawValues)
      ? rawValues.map(String)
      : typeof rawValues === "object" && rawValues !== null
        ? Object.values(rawValues).map(String)
        : undefined;
    const normalized: CapturedFeature = {
      ...feature,
      ...(values ? { values: [...new Set(values)] } : {}),
    };
    this.#features.set(feature.name, structuredClone(normalized));
    return normalized;
  }

  public list(): CapturedFeature[] {
    return [...this.#features.values()].sort((left, right) =>
      left.name.localeCompare(right.name),
    );
  }
}
