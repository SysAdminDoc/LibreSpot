using System.Collections.ObjectModel;

namespace LibreSpot.Desktop.Models;

public sealed class InstallConfiguration
{
    public string Mode { get; set; } = "Easy";
    public bool CleanInstall { get; set; } = true;
    public bool LaunchAfter { get; set; } = true;

    public bool SpotX_NewTheme { get; set; } = true;
    public bool SpotX_PodcastsOff { get; set; } = true;
    public bool SpotX_BlockUpdate { get; set; } = true;
    public bool SpotX_AdSectionsOff { get; set; } = true;
    public bool SpotX_Premium { get; set; }
    public bool SpotX_LyricsEnabled { get; set; } = true;
    public string SpotX_LyricsTheme { get; set; } = "spotify";
    public bool SpotX_TopSearch { get; set; }
    public bool SpotX_RightSidebarOff { get; set; }
    public bool SpotX_RightSidebarClr { get; set; }
    public bool SpotX_CanvasHomeOff { get; set; }
    public bool SpotX_HomeSubOff { get; set; }
    public bool SpotX_DisableStartup { get; set; } = true;
    public bool SpotX_NoShortcut { get; set; }
    public int SpotX_CacheLimit { get; set; }
    public bool SpotX_Plus { get; set; }
    public bool SpotX_NewFullscreen { get; set; }
    public bool SpotX_FunnyProgress { get; set; }
    public bool SpotX_ExpSpotify { get; set; }
    public bool SpotX_LyricsBlock { get; set; }
    public bool SpotX_OldLyrics { get; set; }
    public bool SpotX_HideColIconOff { get; set; }

    public string Spicetify_Theme { get; set; } = "(None - Marketplace Only)";
    public string Spicetify_Scheme { get; set; } = "Default";
    public bool Spicetify_Marketplace { get; set; } = true;
    public List<string> Spicetify_Extensions { get; set; } = new() { "fullAppDisplay.js", "shuffle+.js", "trashbin.js" };

    public InstallConfiguration Clone() =>
        new()
        {
            Mode = Mode,
            CleanInstall = CleanInstall,
            LaunchAfter = LaunchAfter,
            SpotX_NewTheme = SpotX_NewTheme,
            SpotX_PodcastsOff = SpotX_PodcastsOff,
            SpotX_BlockUpdate = SpotX_BlockUpdate,
            SpotX_AdSectionsOff = SpotX_AdSectionsOff,
            SpotX_Premium = SpotX_Premium,
            SpotX_LyricsEnabled = SpotX_LyricsEnabled,
            SpotX_LyricsTheme = SpotX_LyricsTheme,
            SpotX_TopSearch = SpotX_TopSearch,
            SpotX_RightSidebarOff = SpotX_RightSidebarOff,
            SpotX_RightSidebarClr = SpotX_RightSidebarClr,
            SpotX_CanvasHomeOff = SpotX_CanvasHomeOff,
            SpotX_HomeSubOff = SpotX_HomeSubOff,
            SpotX_DisableStartup = SpotX_DisableStartup,
            SpotX_NoShortcut = SpotX_NoShortcut,
            SpotX_CacheLimit = SpotX_CacheLimit,
            SpotX_Plus = SpotX_Plus,
            SpotX_NewFullscreen = SpotX_NewFullscreen,
            SpotX_FunnyProgress = SpotX_FunnyProgress,
            SpotX_ExpSpotify = SpotX_ExpSpotify,
            SpotX_LyricsBlock = SpotX_LyricsBlock,
            SpotX_OldLyrics = SpotX_OldLyrics,
            SpotX_HideColIconOff = SpotX_HideColIconOff,
            Spicetify_Theme = Spicetify_Theme,
            Spicetify_Scheme = Spicetify_Scheme,
            Spicetify_Marketplace = Spicetify_Marketplace,
            Spicetify_Extensions = new List<string>(Spicetify_Extensions ?? [])
        };
}

public sealed record OptionDefinition(string Key, string Title, string Description, string Section);
public sealed record ExtensionDefinition(string Key, string Title, string Description);
public sealed record MaintenanceActionDefinition(string Action, string Title, string Description, string ButtonText, bool IsDestructive = false);

public static class Prettify
{
    /// <summary>
    /// Converts a kebab/snake slug into a human label for display in dropdowns.
    /// Preserves parenthesized sentinels like "(None - Marketplace Only)" verbatim.
    /// </summary>
    public static string Label(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return slug;
        }

        if (slug.StartsWith("(", StringComparison.Ordinal))
        {
            return slug;
        }

        var replaced = slug
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Replace('+', ' ')
            .Replace(".js", string.Empty, StringComparison.OrdinalIgnoreCase);

        var parts = replaced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            if (part.Length <= 3 && part.All(char.IsUpper))
            {
                continue;
            }

            parts[i] = char.ToUpperInvariant(part[0]) + part[1..];
        }

        return string.Join(' ', parts);
    }
}

public sealed class EnvironmentSnapshot
{
    public bool SpotifyInstalled { get; init; }
    public bool SpicetifyInstalled { get; init; }
    public bool SavedConfigExists { get; init; }
    public bool ConfigFolderExists { get; init; }

    public string StatusTitle =>
        SpotifyInstalled && SpicetifyInstalled
            ? "Stack ready"
            : SpotifyInstalled
                ? "Partial setup"
                : "Clean slate";

    public string StatusDetail =>
        SpotifyInstalled && SpicetifyInstalled
            ? "Install, refresh, or roll back from here."
            : SpotifyInstalled
                ? "Spotify is present; customization layer is incomplete."
                : "LibreSpot will build a fresh SpotX + Spicetify stack.";
}

public static class AppCatalog
{
    public static IReadOnlyList<string> LyricsThemes { get; } = new ReadOnlyCollection<string>(new[]
    {
        "spotify", "blueberry", "blue", "discord", "forest", "fresh", "github", "lavender",
        "orange", "pumpkin", "purple", "red", "strawberry", "turquoise", "yellow", "oceano",
        "royal", "krux", "pinkle", "zing", "radium", "sandbar", "postlight", "relish",
        "drot", "default", "spotify#2"
    });

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ThemeSchemes { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            ["(None - Marketplace Only)"] = new[] { "Default" },
            ["Sleek"] = new[] { "Wealthy", "Cherry", "Coral", "Deep", "Greener", "Deeper", "Psycho", "UltraBlack", "Nord", "Futura", "Elementary", "BladeRunner", "Dracula", "VantaBlack", "RosePine", "Eldritch", "Catppuccin", "AyuDark", "TokyoNight" },
            ["Dribbblish"] = new[] { "base", "white", "dark", "dracula", "nord-light", "nord-dark", "purple", "samurai", "beach-sunset", "gruvbox", "gruvbox-material-dark", "rosepine", "lunar", "catppuccin-latte", "catppuccin-frappe", "catppuccin-macchiato", "catppuccin-mocha", "tokyo-night", "kanagawa" },
            ["Ziro"] = new[] { "blue-dark", "blue-light", "gray-dark", "gray-light", "green-dark", "green-light", "orange-dark", "orange-light", "purple-dark", "purple-light", "red-dark", "red-light", "rose-pine", "rose-pine-moon", "rose-pine-dawn", "tokyo-night" },
            ["text"] = new[] { "Spotify", "Spicetify", "CatppuccinMocha", "CatppuccinMacchiato", "CatppuccinLatte", "Dracula", "Gruvbox", "Kanagawa", "Nord", "Rigel", "RosePine", "RosePineMoon", "RosePineDawn", "Solarized", "TokyoNight", "TokyoNightStorm", "ForestGreen", "EverforestDarkHard", "EverforestDarkMedium", "EverforestDarkSoft" },
            ["StarryNight"] = new[] { "Base", "Cotton-candy", "Forest", "Galaxy", "Orange", "Sky", "Sunrise" },
            ["Turntable"] = new[] { "turntable" },
            ["Blackout"] = new[] { "def" },
            ["Blossom"] = new[] { "dark" },
            ["BurntSienna"] = new[] { "Base" },
            ["Default"] = new[] { "Ocean" },
            ["Dreary"] = new[] { "Psycho", "Deeper", "BIB", "Mono", "Golden", "Graytone-Blue" },
            ["Flow"] = new[] { "Pink", "Green", "Silver", "Violet", "Ocean" },
            ["Matte"] = new[] { "matte", "periwinkle", "periwinkle-dark", "porcelain", "rose-pine-moon", "gray-dark1", "gray-dark2", "gray-dark3", "gray", "gray-light" },
            ["Nightlight"] = new[] { "Nightlight Colors" },
            ["Onepunch"] = new[] { "dark", "light", "legacy" },
            ["SharkBlue"] = new[] { "Base" }
        };

    public static IReadOnlyList<OptionDefinition> OptionDefinitions { get; } = new ReadOnlyCollection<OptionDefinition>(new[]
    {
        new OptionDefinition(nameof(InstallConfiguration.CleanInstall), "Remove current stack first", "Clear old Spotify and customization remnants before LibreSpot rebuilds the setup.", "Install"),
        new OptionDefinition(nameof(InstallConfiguration.LaunchAfter), "Open Spotify when finished", "Launch Spotify automatically after LibreSpot completes.", "Install"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_NewTheme), "Enable SpotX new theme", "Apply the newer SpotX shell tweaks for a cleaner Spotify frame.", "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_PodcastsOff), "Hide podcasts", "Reduce podcast surfaces across Spotify for a music-first setup.", "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_BlockUpdate), "Block Spotify updates", "Keep Spotify pinned so your setup is less likely to break unexpectedly.", "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_AdSectionsOff), "Remove ad sections", "Strip ad-promoted sections from key Spotify views.", "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_Premium), "Premium account mode", "Skip ad-focused patching while keeping the rest of the LibreSpot stack.", "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_DisableStartup), "Disable startup launch", "Stop Spotify from auto-launching with Windows.", "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_NoShortcut), "Skip shortcut creation", "Avoid desktop shortcut clutter during install.", "Core"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_LyricsEnabled), "Enable lyrics patch", "Turn on patched lyrics support and choose a lyrics skin.", "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_TopSearch), "Top search bar", "Move Spotify search into a more always-available position.", "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_RightSidebarOff), "Hide right sidebar", "Reduce secondary chrome for a more focused playback layout.", "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_RightSidebarClr), "Clear right sidebar styling", "Use lighter styling on the right sidebar instead of hiding it.", "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_CanvasHomeOff), "Disable home canvas", "Reduce motion and canvas clutter on the Spotify home surface.", "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_HomeSubOff), "Hide home suggestions", "Remove some of Spotify's recommendation-heavy home modules.", "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_OldLyrics), "Old lyrics layout", "Switch back to the earlier lyrics layout treatment.", "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_HideColIconOff), "Keep collection icon visible", "Prevent SpotX from hiding collection affordances.", "Interface"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_Plus), "Expanded SpotX tweaks", "Apply the broader SpotX tweak bundle for a more heavily modified Spotify shell.", "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_NewFullscreen), "Alternative fullscreen layout", "Enable SpotX's alternate fullscreen playback experience.", "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_FunnyProgress), "Novelty progress bar", "Swap in SpotX's playful progress bar variant.", "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_ExpSpotify), "Experimental Spotify features", "Allow experimental Spotify flags when SpotX supports them.", "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.SpotX_LyricsBlock), "Block lyric overlays", "Disable some lyric-related overlays in patched states.", "Advanced"),
        new OptionDefinition(nameof(InstallConfiguration.Spicetify_Marketplace), "Install Marketplace", "Include the Spicetify Marketplace custom app by default.", "Experience")
    });

    public static IReadOnlyList<ExtensionDefinition> ExtensionDefinitions { get; } = new ReadOnlyCollection<ExtensionDefinition>(new[]
    {
        new ExtensionDefinition("fullAppDisplay.js", "Full App Display", "Turn album art and playback controls into a full-screen presentation."),
        new ExtensionDefinition("shuffle+.js", "True Shuffle", "Use a Fisher-Yates shuffle instead of Spotify's weighted shuffle."),
        new ExtensionDefinition("trashbin.js", "Trash Bin", "Skip songs and artists you mark as unwanted."),
        new ExtensionDefinition("keyboardShortcut.js", "Keyboard Shortcuts", "Add Vim-style navigation for faster keyboard control."),
        new ExtensionDefinition("bookmark.js", "Bookmarks", "Save and recall pages, tracks, albums, and timestamps."),
        new ExtensionDefinition("loopyLoop.js", "A-B Loops", "Loop exact track segments for practice or repeat listening."),
        new ExtensionDefinition("popupLyrics.js", "Popup Lyrics", "Open synchronized lyrics in a separate resizable window."),
        new ExtensionDefinition("autoSkipVideo.js", "Skip Video", "Automatically skip canvas videos and unsupported visual content."),
        new ExtensionDefinition("autoSkipExplicit.js", "Skip Explicit", "Automatically skip tracks flagged as explicit."),
        new ExtensionDefinition("webnowplaying.js", "WebNowPlaying", "Expose now-playing data for desktop integrations and widgets.")
    });

    public static IReadOnlyList<MaintenanceActionDefinition> MaintenanceActions { get; } = new ReadOnlyCollection<MaintenanceActionDefinition>(new[]
    {
        new MaintenanceActionDefinition("CheckUpdates", "Check pinned versions", "Compare LibreSpot's pinned dependencies against upstream releases before you update the stack.", "Check versions"),
        new MaintenanceActionDefinition("Reapply", "Reapply your setup", "Refresh SpotX first and then restore the saved Spicetify theme and extension state.", "Reapply"),
        new MaintenanceActionDefinition("RestoreVanilla", "Restore vanilla Spotify", "Remove active Spicetify customizations while leaving SpotX in place.", "Restore"),
        new MaintenanceActionDefinition("UninstallSpicetify", "Uninstall Spicetify", "Restore Spotify and then remove the Spicetify CLI, config folder, and PATH entry.", "Remove Spicetify", true),
        new MaintenanceActionDefinition("FullReset", "Full reset", "Remove SpotX, Spicetify, Spotify app state, and related leftovers for a truly clean start.", "Reset everything", true)
    });

    public static IReadOnlyList<string> RecommendedHighlights { get; } = new ReadOnlyCollection<string>(new[]
    {
        "Starts from a clean Spotify state so older patches do not leak into the new stack.",
        "Applies the pinned SpotX profile with lyrics enabled and update blocking turned on.",
        "Restores Spicetify, Marketplace, and the starter extensions LibreSpot manages best.",
        "Saves a predictable profile for future reapply, recovery, and maintenance runs."
    });

    public static InstallConfiguration CreateRecommendedConfiguration() => new();

    public static InstallConfiguration NormalizeConfiguration(InstallConfiguration? source)
    {
        var normalized = CreateRecommendedConfiguration().Clone();
        if (source is null)
        {
            return normalized;
        }

        normalized.Mode = source.Mode is "Easy" or "Custom" ? source.Mode : normalized.Mode;
        normalized.CleanInstall = source.CleanInstall;
        normalized.LaunchAfter = source.LaunchAfter;

        normalized.SpotX_NewTheme = source.SpotX_NewTheme;
        normalized.SpotX_PodcastsOff = source.SpotX_PodcastsOff;
        normalized.SpotX_BlockUpdate = source.SpotX_BlockUpdate;
        normalized.SpotX_AdSectionsOff = source.SpotX_AdSectionsOff;
        normalized.SpotX_Premium = source.SpotX_Premium;
        normalized.SpotX_LyricsEnabled = source.SpotX_LyricsEnabled;
        normalized.SpotX_TopSearch = source.SpotX_TopSearch;
        normalized.SpotX_RightSidebarOff = source.SpotX_RightSidebarOff;
        normalized.SpotX_RightSidebarClr = source.SpotX_RightSidebarClr && !source.SpotX_RightSidebarOff;
        normalized.SpotX_CanvasHomeOff = source.SpotX_CanvasHomeOff;
        normalized.SpotX_HomeSubOff = source.SpotX_HomeSubOff;
        normalized.SpotX_DisableStartup = source.SpotX_DisableStartup;
        normalized.SpotX_NoShortcut = source.SpotX_NoShortcut;
        normalized.SpotX_CacheLimit = Math.Clamp(source.SpotX_CacheLimit, 0, 50_000);
        normalized.SpotX_Plus = source.SpotX_Plus;
        normalized.SpotX_NewFullscreen = source.SpotX_NewFullscreen;
        normalized.SpotX_FunnyProgress = source.SpotX_FunnyProgress;
        normalized.SpotX_ExpSpotify = source.SpotX_ExpSpotify;
        normalized.SpotX_LyricsBlock = source.SpotX_LyricsBlock;
        normalized.SpotX_OldLyrics = source.SpotX_OldLyrics;
        normalized.SpotX_HideColIconOff = source.SpotX_HideColIconOff;

        normalized.SpotX_LyricsTheme = !string.IsNullOrWhiteSpace(source.SpotX_LyricsTheme) && LyricsThemes.Contains(source.SpotX_LyricsTheme)
            ? source.SpotX_LyricsTheme
            : normalized.SpotX_LyricsTheme;

        normalized.Spicetify_Theme = !string.IsNullOrWhiteSpace(source.Spicetify_Theme) && ThemeSchemes.ContainsKey(source.Spicetify_Theme)
            ? source.Spicetify_Theme
            : normalized.Spicetify_Theme;

        var validSchemes = ThemeSchemes.TryGetValue(normalized.Spicetify_Theme, out var schemes)
            ? schemes
            : ThemeSchemes[normalized.Spicetify_Theme];

        normalized.Spicetify_Scheme = validSchemes.Contains(source.Spicetify_Scheme)
            ? source.Spicetify_Scheme
            : validSchemes.First();

        normalized.Spicetify_Marketplace = source.Spicetify_Marketplace;

        var validExtensions = ExtensionDefinitions.Select(def => def.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        normalized.Spicetify_Extensions = (source.Spicetify_Extensions ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item) && validExtensions.Contains(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized;
    }
}
