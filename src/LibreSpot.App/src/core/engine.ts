import {
  resolveAccent,
  type ColorExtractor,
  type AccentResult,
} from "./accent.ts";
import { deriveScheme, type ColorScheme } from "./colors.ts";
import {
  applyFeatureOverrides,
  type FeatureOverrideRuntime,
  type FeatureValue,
} from "./feature-overrides.ts";
import {
  browserFrameClock,
  classifyFrameRate,
  prefersReducedMotion,
  probeFrameRate,
} from "./performance.ts";
import { resolveScheduledScheme } from "./schedule.ts";
import { ManagedRuntimeStyles } from "./runtime-styles.ts";
import type { EngineStore } from "./store.ts";
import { cloneState, type EngineState } from "./state.ts";

export type EngineEnvironment = {
  document: Document;
  window: Window;
  store: EngineStore;
  initialState: EngineState;
  snippetCss?: Readonly<Record<string, string>>;
  themeStyles?: Readonly<Record<string, { className: string; css: string }>>;
  featureRuntime?: FeatureOverrideRuntime;
  colorExtractor?: ColorExtractor;
  artworkUri?: () => string | undefined;
  osAccent?: () => string | null;
  now?: () => Date;
};

export type EngineAppliedDetail = {
  state: EngineState;
  activeScheme: string;
};

export class LibreSpotEngine extends EventTarget {
  readonly #styles: ManagedRuntimeStyles;
  readonly #document: Document;
  readonly #window: Window;
  readonly #store: EngineStore;
  readonly #featureRuntime: FeatureOverrideRuntime | undefined;
  readonly #colorExtractor: ColorExtractor | undefined;
  readonly #artworkUri: (() => string | undefined) | undefined;
  readonly #osAccent: (() => string | null) | undefined;
  readonly #now: () => Date;
  #snippetCss: Readonly<Record<string, string>>;
  readonly #themeStyles: Readonly<Record<string, { className: string; css: string }>>;
  #state: EngineState;
  #activeScheme: string;

  public constructor(environment: EngineEnvironment) {
    super();
    this.#document = environment.document;
    this.#window = environment.window;
    this.#store = environment.store;
    this.#state = cloneState(environment.initialState);
    this.#activeScheme = this.#state.scheme;
    this.#snippetCss = environment.snippetCss ?? {};
    this.#themeStyles = environment.themeStyles ?? {};
    this.#featureRuntime = environment.featureRuntime;
    this.#colorExtractor = environment.colorExtractor;
    this.#artworkUri = environment.artworkUri;
    this.#osAccent = environment.osAccent;
    this.#now = environment.now ?? (() => new Date());
    this.#styles = new ManagedRuntimeStyles(this.#document);
  }

  public get state(): EngineState {
    return cloneState(this.#state);
  }

  public get activeScheme(): string {
    return this.#activeScheme;
  }

  public async start(
    options: {
      probePerformance?: boolean;
      previousFeatureOverrides?: Readonly<Record<string, FeatureValue>>;
    } = {},
  ): Promise<void> {
    this.#styles.installLayerStyles();
    this.#styles.setReducedMotion(prefersReducedMotion(this.#window));
    this.apply();
    await this.refreshAccent();
    await this.applyFlags(options.previousFeatureOverrides);
    if (options.probePerformance ?? true) {
      await this.probePerformance();
    }
  }

  public apply(): void {
    this.#activeScheme = resolveScheduledScheme(this.#state, this.#now());
    const scheme = this.#state.schemes[this.#activeScheme];
    if (!scheme) {
      throw new Error(`Active scheme "${this.#activeScheme}" is not available.`);
    }
    this.#styles.applyPalette(scheme, this.#state.layers.palette);
    this.#styles.applyLayers(this.#state.layers, this.#state.effectsTier);
    this.#styles.applyAppearance(this.#state);
    const theme = this.#themeStyles[this.#state.theme];
    this.#styles.applyTheme(theme?.className ?? "", theme?.css ?? "");
    this.#styles.setHighContrast(
      this.#state.layers.accessibility &&
        this.#activeScheme.toLowerCase().includes("contrast"),
    );
    const snippets = this.#state.enabledSnippets.flatMap((id) => {
      const css = this.#snippetCss[id];
      return css ? [`/* ${id} */\n${css}`] : [];
    });
    this.#styles.applySnippets(snippets);
    this.dispatchEvent(
      new CustomEvent<EngineAppliedDetail>("applied", {
        detail: {
          state: this.state,
          activeScheme: this.#activeScheme,
        },
      }),
    );
  }

  public update(mutator: (draft: EngineState) => void): EngineState {
    const draft = cloneState(this.#state);
    mutator(draft);
    this.#state = this.#store.save(draft);
    this.apply();
    return this.state;
  }

  public replace(state: EngineState): EngineState {
    this.#state = this.#store.save(state);
    this.apply();
    return this.state;
  }

  public setSnippetCatalog(catalog: Readonly<Record<string, string>>): void {
    this.#snippetCss = catalog;
    this.apply();
  }

  public async refreshAccent(): Promise<AccentResult> {
    const base = this.#state.schemes[this.#activeScheme];
    if (!base) {
      throw new Error(`Active scheme "${this.#activeScheme}" is not available.`);
    }
    const derived = deriveScheme(base);
    const result = await resolveAccent(this.#state, derived, {
      artworkUri: this.#artworkUri?.(),
      extractor: this.#colorExtractor,
      osAccent: this.#osAccent?.(),
      isDark: !this.#activeScheme.toLowerCase().includes("light"),
    });
    if (result.scheme && this.#state.layers.palette) {
      this.#styles.applyPalette(result.scheme);
    }
    this.#styles.setAccent(result.accent);
    return result;
  }

  public async applyFlags(
    previousOverrides: Readonly<Record<string, FeatureValue>> = {},
  ): Promise<
    "debug-api" | "resolver" | "unavailable"
  > {
    if (!this.#featureRuntime) {
      return "unavailable";
    }
    return await applyFeatureOverrides(
      this.#state.featureOverrides,
      this.#featureRuntime,
      previousOverrides,
    );
  }

  public async probePerformance(): Promise<number | null> {
    if (!this.#state.autoEffects || this.#state.effectsTier !== "glass") {
      return this.#state.lastMeasuredFps;
    }
    const fps = await probeFrameRate(browserFrameClock(this.#window));
    this.update((draft) => {
      draft.lastMeasuredFps = fps;
      const recommended = classifyFrameRate(fps);
      if (recommended !== "glass") {
        draft.effectsTier = recommended;
      }
    });
    return fps;
  }

  public applyPreviewScheme(scheme: ColorScheme): void {
    this.#styles.applyPalette(scheme, this.#state.layers.palette);
  }

  public applyPreviewTheme(themeName: string, schemeName?: string): boolean {
    const theme = this.#themeStyles[themeName];
    if (!theme) return false;
    this.#styles.applyTheme(theme.className, theme.css);
    const scheme = schemeName ? this.#state.schemes[schemeName] : undefined;
    if (scheme) {
      this.#styles.applyPalette(scheme, this.#state.layers.palette);
      this.#styles.setHighContrast(
        this.#state.layers.accessibility && Boolean(schemeName?.toLowerCase().includes("contrast")),
      );
    }
    return true;
  }

  public clearPreview(): void {
    this.apply();
  }

  public stop(): void {
    this.#styles.dispose();
  }
}
