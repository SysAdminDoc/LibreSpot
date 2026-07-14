using System.IO;

namespace LibreSpot.Desktop.Services;

public enum ShellActivationKind
{
    None,
    NavigateRecommended,
    NavigateCustom,
    NavigateMaintenance,
    ImportProfile,
    OpenLibreSpotFolder,
    ProfileFile,
    ProfileShareUri
}

public sealed record ShellActivationRequest(ShellActivationKind Kind, string? Value = null)
{
    public static ShellActivationRequest None { get; } = new(ShellActivationKind.None);
    public bool HasActivation => Kind != ShellActivationKind.None;
}

public static class ShellActivationService
{
    public const string ShellActionArgumentPrefix = "--shell-action=";
    public const string ProfileUriArgumentPrefix = "--profile-uri=";
    public const string ProfileFileArgumentPrefix = "--profile-file=";
    public const string ProfileFileArgument = "--profile-file";
    public const string ProfileUriPrefix = "librespot://profile";
    public const string ProfileExtension = ".librespot";

    public static ShellActivationRequest Parse(IEnumerable<string> args)
    {
        var expectProfileFile = false;

        foreach (var rawArg in args)
        {
            var arg = (rawArg ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (expectProfileFile)
            {
                return BuildProfileFileActivation(arg);
            }

            if (arg.Equals(ProfileFileArgument, StringComparison.OrdinalIgnoreCase))
            {
                expectProfileFile = true;
                continue;
            }

            if (arg.StartsWith(ShellActionArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return ParseShellAction(arg[ShellActionArgumentPrefix.Length..]);
            }

            if (arg.StartsWith(ProfileUriArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[ProfileUriArgumentPrefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(value)
                    ? ShellActivationRequest.None
                    : new ShellActivationRequest(ShellActivationKind.ProfileShareUri, value);
            }

            if (arg.StartsWith(ProfileFileArgumentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return BuildProfileFileActivation(arg[ProfileFileArgumentPrefix.Length..].Trim());
            }

            if (arg.StartsWith(ProfileUriPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new ShellActivationRequest(ShellActivationKind.ProfileShareUri, arg);
            }

            if (string.Equals(Path.GetExtension(arg), ProfileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return BuildProfileFileActivation(arg);
            }
        }

        return ShellActivationRequest.None;
    }

    private static ShellActivationRequest BuildProfileFileActivation(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? ShellActivationRequest.None
            : new ShellActivationRequest(ShellActivationKind.ProfileFile, path);
    }

    private static ShellActivationRequest ParseShellAction(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "recommended" or "home" => new ShellActivationRequest(ShellActivationKind.NavigateRecommended),
            "custom" => new ShellActivationRequest(ShellActivationKind.NavigateCustom),
            "maintenance" or "repair" => new ShellActivationRequest(ShellActivationKind.NavigateMaintenance),
            "import" or "import-profile" => new ShellActivationRequest(ShellActivationKind.ImportProfile),
            "folder" or "open-folder" or "logs" => new ShellActivationRequest(ShellActivationKind.OpenLibreSpotFolder),
            _ => ShellActivationRequest.None
        };
    }
}
