namespace LibreSpot.Desktop.Services;

/// <summary>
/// Well-known per-user and machine LibreSpot directories. Keep these in one
/// place so writers and readers cannot drift onto different folder names.
/// </summary>
public static class LibreSpotPaths
{
    public const string DirectoryName = "LibreSpot";

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DirectoryName);

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static string LogsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DirectoryName, "logs");

    public static string CrashesDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DirectoryName, "crashes");

    public static string MachineConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), DirectoryName);

    public static string MachineConfigPath => Path.Combine(MachineConfigDirectory, "config.json");

    public static string MachineLogsDirectory => Path.Combine(MachineConfigDirectory, "logs");

    public static string RuntimeDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DirectoryName, "Runtime");
}
